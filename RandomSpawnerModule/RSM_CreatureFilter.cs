using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UWE;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Responsible for filtering the raw creature list produced by RSM_CreatureRegistry.
    /// Removes creatures that are unsuitable for random spawning based on :
    ///   1. Name exclusion keywords
    ///   2. Size limits (average of axes via collider geometry)
    /// The average of the 3 axes is used as the magnitude metric to normalize
    /// elongated creatures (e.g. Crabsnake, Shocker) against rounder ones.
    /// 
    /// Collider size is measured without activating the creature instance,
    /// preventing BehaviourLOD.Update() from firing in the XMenu scene
    /// where the Streamer is absent.
    /// </summary>
    public static class RSM_CreatureFilter
    {
        // Constants

        public const float SIZE_MAGNITUDE_LIMIT = 165f;
        public const float SIZE_LENGTH_LIMIT = float.MaxValue;

        // Private state

        private static List<(TechType techType, float magnitude)> filteredCreatures
            = new List<(TechType techType, float magnitude)>();

        // Public API

        /// Filters the raw creature list by name, then measures the size of each remaining creature via collider geometry.
        public static IEnumerator Filter(List<TechType> rawCreatures, Action onCompleted)
        {
            filteredCreatures.Clear();

            int totalInput = rawCreatures.Count;
            int excludedByName = 0;
            int excludedBySize = 0;

            // Step 1 : name exclusion
            List<TechType> namePassedCreatures = new List<TechType>();

            foreach (TechType techType in rawCreatures)
            {
                string name = techType.ToString().ToLower();

                if (IsNameExcluded(name))
                {
                    Plugin.Log.LogDebug($"[RSM_CreatureFilter] {techType} excluded by name rule.");
                    excludedByName++;
                    continue;
                }

                namePassedCreatures.Add(techType);
            }

            Plugin.Log.LogInfo($"[RSM_CreatureFilter] Name filter done : " +
                               $"{namePassedCreatures.Count} remaining after {excludedByName} name exclusions.");

            // Step 2 : size measurement and filter

            foreach (TechType techType in namePassedCreatures)
            {
                var task = CraftData.GetPrefabForTechTypeAsync(techType, verbose: false);
                yield return task;

                GameObject prefab = task.GetResult();

                if (prefab == null)
                {
                    Plugin.Log.LogWarning($"[RSM_CreatureFilter] Could not load prefab for {techType} : keeping with magnitude 0.");
                    filteredCreatures.Add((techType, 0f));
                    continue;
                }

                GameObject instance = UnityEngine.Object.Instantiate(prefab);

                // Measure size using collider geometry
                Vector3 size = GetColliderSize(instance, techType);
                float magnitude = (size.x + size.y + size.z) / 3f;
                float maxAxis = Mathf.Max(size.x, size.y, size.z);

                // Destroy the temporary instance immediately after measuring
                UnityEngine.Object.Destroy(instance);

                // Apply size filter
                bool tooLarge = magnitude > SIZE_MAGNITUDE_LIMIT;
                bool tooLong = maxAxis > SIZE_LENGTH_LIMIT;

                if (tooLarge || tooLong)
                {
                    Plugin.Log.LogDebug($"[RSM_CreatureFilter] {techType} excluded by size rule " +
                                        $"(magnitude={magnitude:F2}, maxAxis={maxAxis:F2}).");
                    excludedBySize++;
                    continue;
                }

                // Store creature with its measured magnitude
                filteredCreatures.Add((techType, magnitude));
            }

            // Final summary
            Plugin.Log.LogInfo($"[RSM_CreatureFilter] Filtering complete : " +
                               $"{filteredCreatures.Count} creatures kept, " +
                               $"{excludedByName} excluded by name, " +
                               $"{excludedBySize} excluded by size, " +
                               $"out of {totalInput} total.");

            Plugin.Log.LogInfo($"[RSM_CreatureFilter] Final list : {string.Join(", ", filteredCreatures)}");

            onCompleted?.Invoke();
        }

        /// Returns a copy of the filtered creature list with their magnitudes.
        public static List<(TechType techType, float magnitude)> GetFilteredCreatures()
        {
            return new List<(TechType techType, float magnitude)>(filteredCreatures);
        }

        /// Returns the number of available creatures after filtering.
        public static int Count => filteredCreatures.Count;

        /// Clears the filtered creature list.
        public static void Clear()
        {
            Plugin.Log.LogInfo("[RSM_CreatureFilter] Clearing filtered creature list.");
            filteredCreatures.Clear();
        }

        // Private helpers

        /// Returns true if the creature name contains any excluded keyword.
        private static bool IsNameExcluded(string name)
        {
            foreach (string keyword in LPS_Config.ExcludedKeywords)
            {
                if (name.Contains(keyword))
                    return true;
            }
            return false;
        }

        /// Computes the combined size of all colliders on a creature instance
        /// by reading their intrinsic geometry — no activation required.
        /// Supports BoxCollider, SphereCollider, CapsuleCollider, and MeshCollider.
        private static Vector3 GetColliderSize(GameObject instance, TechType techType)
        {
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);

            if (colliders.Length == 0)
            {
                Plugin.Log.LogWarning($"[RSM_CreatureFilter] No colliders found on {techType} : size reported as zero.");
                return Vector3.zero;
            }

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool anyValid = false;

            foreach (Collider col in colliders)
            {
                Vector3 colMin, colMax;

                if (col is BoxCollider box)
                {
                    Vector3 halfSize = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f;
                    halfSize = new Vector3(Mathf.Abs(halfSize.x), Mathf.Abs(halfSize.y), Mathf.Abs(halfSize.z));
                    Vector3 center = box.transform.TransformPoint(box.center);
                    colMin = center - halfSize;
                    colMax = center + halfSize;
                }
                else if (col is SphereCollider sphere)
                {
                    float radius = sphere.radius * Mathf.Max(
                        Mathf.Abs(sphere.transform.lossyScale.x),
                        Mathf.Abs(sphere.transform.lossyScale.y),
                        Mathf.Abs(sphere.transform.lossyScale.z));
                    Vector3 center = sphere.transform.TransformPoint(sphere.center);
                    colMin = center - Vector3.one * radius;
                    colMax = center + Vector3.one * radius;
                }
                else if (col is CapsuleCollider capsule)
                {
                    float scale = Mathf.Max(
                        Mathf.Abs(capsule.transform.lossyScale.x),
                        Mathf.Abs(capsule.transform.lossyScale.y),
                        Mathf.Abs(capsule.transform.lossyScale.z));
                    float radius = capsule.radius * scale;
                    float halfHeight = Mathf.Max(capsule.height * scale * 0.5f, radius);
                    Vector3 center = capsule.transform.TransformPoint(capsule.center);
                    colMin = center - new Vector3(radius, halfHeight, radius);
                    colMax = center + new Vector3(radius, halfHeight, radius);
                }
                else if (col is MeshCollider mesh && mesh.sharedMesh != null)
                {
                    Bounds b = mesh.sharedMesh.bounds;
                    Vector3 s = mesh.transform.lossyScale;
                    Vector3 scaledSize = Vector3.Scale(b.size, new Vector3(
                        Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
                    Vector3 center = mesh.transform.TransformPoint(b.center);
                    colMin = center - scaledSize * 0.5f;
                    colMax = center + scaledSize * 0.5f;
                }
                else
                {
                    // Unknown or unsupported collider type — skip
                    continue;
                }

                min = Vector3.Min(min, colMin);
                max = Vector3.Max(max, colMax);
                anyValid = true;
            }

            if (!anyValid)
            {
                Plugin.Log.LogWarning($"[RSM_CreatureFilter] No supported collider type on {techType} : size reported as zero.");
                return Vector3.zero;
            }

            return max - min;
        }
    }
}
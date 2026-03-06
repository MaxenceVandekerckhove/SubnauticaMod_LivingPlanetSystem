using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UWE;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Responsible for filtering the raw creature list produced by RSM_CreatureRegistry.
    /// Removes creatures that are unsuitable for random spawning based on :
    ///   1. Name exclusion keywords
    ///   2. Size limits (average of axes via collider bounds)
    /// The average of the 3 axes is used as the magnitude metric to normalize
    /// elongated creatures (e.g. Crabsnake, Shocker) against rounder ones.
    /// </summary>
    public static class RSM_CreatureFilter
    {
        // Constants

        public const float SIZE_MAGNITUDE_LIMIT = 60f;
        public const float SIZE_LENGTH_LIMIT = float.MaxValue;

        // Private state

        private static List<(TechType techType, float magnitude)> filteredCreatures
            = new List<(TechType techType, float magnitude)>();

        // Exclusion rules

        /// Creature name keywords that disqualify a creature from random spawning.
        private static readonly string[] ExcludedKeywords =
        {
            "test",
            "example",
            "gargantuan",
            "cutefish",
            "skyray",
            "seaemperor",
            "mrteeth",
            "consciousneuralmatter",
            "meatball",
            "gilbert",
            "silence",
            "dragonfly",
            "bloom"
        };

        // Public API

        /// Filters the raw creature list by name, then measures the size of each remaining creature via instantiated collider bounds.
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
            Plugin.Log.LogInfo("[RSM_CreatureFilter] Starting size measurement...");

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

                // Instantiate and activate so colliders are properly initialized
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                instance.SetActive(true);

                // Wait one frame for physics/colliders to initialize
                yield return null;

                // Measure size using all colliders combined
                Vector3 size = GetColliderSize(instance, techType);
                float magnitude = (size.x + size.y + size.z) / 3f;
                float maxAxis = Mathf.Max(size.x, size.y, size.z);

                // Destroy the temporary instance immediately after measuring
                UnityEngine.Object.Destroy(instance);

                Plugin.Log.LogDebug($"[RSM_CreatureFilter] SIZE | {techType,-30} " +
                                    $"avg={magnitude,7:F2}  maxAxis={maxAxis,7:F2}  " +
                                    $"(x={size.x:F2} y={size.y:F2} z={size.z:F2})");

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
            foreach (string keyword in ExcludedKeywords)
            {
                if (name.Contains(keyword))
                    return true;
            }
            return false;
        }

        /// Computes the combined bounds of all colliders on a creature instance.
        private static Vector3 GetColliderSize(GameObject instance, TechType techType)
        {
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);

            if (colliders.Length == 0)
            {
                Plugin.Log.LogWarning($"[RSM_CreatureFilter] No colliders found on {techType} : size reported as zero.");
                return Vector3.zero;
            }

            Bounds combined = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                combined.Encapsulate(colliders[i].bounds);

            return combined.size;
        }
    }
}
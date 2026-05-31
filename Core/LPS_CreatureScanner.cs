using UnityEngine;
using System;

namespace LivingPlanetSystem.Core
{
    /// <summary>
    /// Debug utility that scans for creatures when F9 is pressed in-game.
    /// Logs the TechType name and collider magnitude of all detected creatures within ScanRadius.
    /// Only active when LPS_Config.DebugScannerEnabled is true.
    /// Attached to the Plugin GameObject via Plugin.OnGameWorldLoaded.
    /// </summary>
    public class LPS_CreatureScanner : MonoBehaviour
    {
        // Constants

        private const KeyCode ScanKey = KeyCode.F10;
        private const float ScanRadius = 200f;

        // Unity lifecycle
        private void Update()
        {
            if (!LPS_Config.DebugScannerEnabled)
                return;

            if (!Input.GetKeyDown(ScanKey))
                return;

            RunScan();
        }

        // Private logic

        private void RunScan()
        {
            GameObject player = Player.main?.gameObject;
            if (player == null)
            {
                Plugin.Log.LogWarning("[LPS_CreatureScanner] Player not found : aborting scan.");
                return;
            }

            Plugin.Log.LogInfo($"[LPS_CreatureScanner] Scanning for creatures within {ScanRadius}m...");

            Collider[] hits = Physics.OverlapSphere(player.transform.position, ScanRadius);

            int found = 0;

            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                Creature creature = hit.GetComponentInParent<Creature>();
                if (creature == null)
                    continue;

                GameObject root = creature.gameObject;

                TechType techType = CraftData.GetTechType(root);
                if (techType == TechType.None)
                    continue;

                float magnitude = GetMagnitude(root, techType);

                Plugin.Log.LogInfo($"[LPS_CreatureScanner] Found : {techType} " +
                                   $"| magnitude={magnitude:F2} " +
                                   $"| position={root.transform.position}");
                found++;
            }

            if (found == 0)
                Plugin.Log.LogWarning($"[LPS_CreatureScanner] No creatures found within {ScanRadius}m.");
            else
                Plugin.Log.LogInfo($"[LPS_CreatureScanner] Scan complete : {found} creature(s) found.");
        }

        // Reuses RSM_CreatureFilter collider logic to measure magnitude.
        private float GetMagnitude(GameObject instance, TechType techType)
        {
            try
            {
                Collider[] colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);

                if (colliders.Length == 0)
                    return 0f;

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
                        continue;

                    min = Vector3.Min(min, colMin);
                    max = Vector3.Max(max, colMax);
                    anyValid = true;
                }

                if (!anyValid)
                    return 0f;

                Vector3 size = max - min;
                return (size.x + size.y + size.z) / 3f;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[LPS_CreatureScanner] Magnitude measurement failed for {techType} : {e.Message}");
                return 0f;
            }
        }
    }
}
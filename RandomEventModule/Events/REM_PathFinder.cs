using System;
using System.Collections;
using LivingPlanetSystem.RandomSpawnerModule;
using UnityEngine;
using WorldHeightLib;

namespace LivingPlanetSystem.RandomEventModule.Events
{
    /// <summary>
    /// Responsible for finding a valid migration path (A → B) for REM events
    /// that require creatures to travel through the player's field of vision.
    ///
    /// Strategy :
    ///   A and B are constructed geometrically so the player-proximity constraint
    ///   is satisfied by design :
    ///     1. Pick a random horizontal direction D around the player.
    ///     2. Compute a lateral perpendicular offset in [-playerPassRadius, +playerPassRadius].
    ///     3. Compute XZ for A = playerPos - D * distanceA + lateralOffset
    ///                  XZ for B = playerPos + D * distanceB + lateralOffset
    ///     4. Resolve Y for A and B independently from HeightMap :
    ///          y = Random.Range(groundHeight + GroundClearance, -GroundClearance)
    ///        This guarantees Y is always underwater and always above terrain.
    ///     5. Validate biome at A and B.
    ///     6. Verify the path length AB >= minPathLength.
    ///     7. Verify no sampled point along AB is below terrain + GroundClearance.
    ///   Retries with a new direction D on any failure.
    /// </summary>
    public static class REM_PathFinder
    {
        // Validation constants

        // Minimum clearance in metres above the terrain for position construction and path sampling.
        private const float GroundClearance = 10f;

        // Number of points sampled along segment AB for terrain validation.
        private const int TerrainSampleCount = 10;

        // Maximum number of full attempts before giving up.
        private const int MaxAttempts = 15;

        // Public data structure

        // Holds the two endpoints of a valid migration path.
        public class MigrationPath
        {
            public Vector3 SpawnPosition { get; set; }
            public Vector3 DestinationPosition { get; set; }
        }

        // Public API

        // Searches for a valid migration path whose segment passes within "playerPassRadius" metres of the player.
        public static IEnumerator Find(
            Action<MigrationPath> onCompleted,
            float distanceA = 150f,
            float distanceB = 250f,
            float minPathLength = 200f,
            float playerPassRadius = 20f)
        {
            // Guard : player must be present
            GameObject player = Player.main?.gameObject;
            if (player == null)
            {
                Plugin.Log.LogWarning("[REM_PathFinder] Player not found : aborting.");
                onCompleted?.Invoke(null);
                yield break;
            }

            // Guard : WorldHeightLib must be available
            if (HeightMap.Instance == null)
            {
                Plugin.Log.LogWarning("[REM_PathFinder] HeightMap.Instance is null : aborting.");
                onCompleted?.Invoke(null);
                yield break;
            }

            Plugin.Log.LogInfo($"[REM_PathFinder] Searching for migration path " +
                               $"(distanceA={distanceA}, distanceB={distanceB}, " +
                               $"minLength={minPathLength}, passRadius={playerPassRadius})...");

            Vector3 playerPos = player.transform.position;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                // 1. Random horizontal direction
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized;
                Vector3 direction = new Vector3(randomCircle.x, 0f, randomCircle.y);

                // 2. Lateral perpendicular offset
                Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x);
                float lateralAmount = UnityEngine.Random.Range(-playerPassRadius, playerPassRadius);
                Vector3 lateralOffset = perpendicular * lateralAmount;

                // 3. Compute XZ for A and B
                Vector3 xzA = playerPos - direction * distanceA + lateralOffset;
                Vector3 xzB = playerPos + direction * distanceB + lateralOffset;

                // 4. Resolve Y for A from HeightMap
                if (!TryResolveY(xzA, out float yA))
                {
                    Plugin.Log.LogDebug($"[REM_PathFinder] Attempt {attempt}/{MaxAttempts} : " +
                                        $"HeightMap failed for A ({xzA.x:F0}, {xzA.z:F0}).");
                    yield return null;
                    continue;
                }

                // 5. Resolve Y for B from HeightMap
                if (!TryResolveY(xzB, out float yB))
                {
                    Plugin.Log.LogDebug($"[REM_PathFinder] Attempt {attempt}/{MaxAttempts} : " +
                                        $"HeightMap failed for B ({xzB.x:F0}, {xzB.z:F0}).");
                    yield return null;
                    continue;
                }

                Vector3 posA = new Vector3(xzA.x, yA, xzA.z);
                Vector3 posB = new Vector3(xzB.x, yB, xzB.z);

                // 6. Validate biome at A
                string biomeA = WaterBiomeManager.main?.GetBiome(posA) ?? string.Empty;
                if (IsBiomeExcluded(biomeA))
                {
                    Plugin.Log.LogDebug($"[REM_PathFinder] Attempt {attempt}/{MaxAttempts} : " +
                                        $"biome '{biomeA}' at A is excluded.");
                    yield return null;
                    continue;
                }

                // 7. Validate biome at B
                string biomeB = WaterBiomeManager.main?.GetBiome(posB) ?? string.Empty;
                if (IsBiomeExcluded(biomeB))
                {
                    Plugin.Log.LogDebug($"[REM_PathFinder] Attempt {attempt}/{MaxAttempts} : " +
                                        $"biome '{biomeB}' at B is excluded.");
                    yield return null;
                    continue;
                }

                // 8. Minimum path length
                float pathLength = Vector3.Distance(posA, posB);
                if (pathLength < minPathLength)
                {
                    Plugin.Log.LogDebug($"[REM_PathFinder] Attempt {attempt}/{MaxAttempts} : " +
                                        $"path too short ({pathLength:F1}m < {minPathLength}m).");
                    yield return null;
                    continue;
                }

                // 9. Terrain clearance along AB
                if (!IsPathClearOfTerrain(posA, posB))
                {
                    Plugin.Log.LogDebug($"[REM_PathFinder] Attempt {attempt}/{MaxAttempts} : " +
                                        $"path intersects terrain.");
                    yield return null;
                    continue;
                }

                // All checks passed
                Plugin.Log.LogInfo($"[REM_PathFinder] Valid path found on attempt {attempt} : " +
                                   $"A={posA}, B={posB}, length={pathLength:F1}m, " +
                                   $"lateral={lateralAmount:F1}m.");

                onCompleted?.Invoke(new MigrationPath
                {
                    SpawnPosition = posA,
                    DestinationPosition = posB
                });
                yield break;
            }

            Plugin.Log.LogWarning($"[REM_PathFinder] No valid path found after {MaxAttempts} attempts.");
            onCompleted?.Invoke(null);
        }

        // Private helpers

        // Queries HeightMap at the XZ position and resolves a valid Yrandomly in [groundHeight + GroundClearance, -GroundClearance].
        private static bool TryResolveY(Vector3 xzPos, out float y)
        {
            y = 0f;

            if (!HeightMap.Instance.TryGetValueAtPosition(
                    new Vector2(xzPos.x, xzPos.z), out float groundHeight))
                return false;

            float yMin = groundHeight + GroundClearance;
            float yMax = -GroundClearance;

            // No valid water column at this XZ position
            if (yMin >= yMax)
            {
                Plugin.Log.LogDebug($"[REM_PathFinder] No valid water column at " +
                                    $"({xzPos.x:F0}, {xzPos.z:F0}) " +
                                    $"(ground={groundHeight:F1}, yMin={yMin:F1} >= yMax={yMax:F1}).");
                return false;
            }

            y = UnityEngine.Random.Range(yMin, yMax);
            return true;
        }

        // Samples TerrainSampleCount evenly spaced points along AB and returns
        private static bool IsPathClearOfTerrain(Vector3 a, Vector3 b)
        {
            for (int i = 0; i <= TerrainSampleCount; i++)
            {
                float t = (float)i / TerrainSampleCount;
                Vector3 point = Vector3.Lerp(a, b, t);

                if (!HeightMap.Instance.TryGetValueAtPosition(
                        new Vector2(point.x, point.z), out float groundHeight))
                {
                    Plugin.Log.LogDebug($"[REM_PathFinder] HeightMap returned no data at " +
                                        $"({point.x:F0}, {point.z:F0}) : rejecting path.");
                    return false;
                }

                if (point.y <= groundHeight + GroundClearance)
                {
                    Plugin.Log.LogDebug($"[REM_PathFinder] Sample {i}/{TerrainSampleCount} " +
                                        $"at y={point.y:F1} too close to ground " +
                                        $"(ground={groundHeight:F1}, " +
                                        $"required above {groundHeight + GroundClearance:F1}) : rejecting path.");
                    return false;
                }
            }

            return true;
        }

        // Returns true if the biome name contains any keyword from RSM_BiomeRegistry.ExcludedKeywords.
        private static bool IsBiomeExcluded(string biome)
        {
            if (string.IsNullOrEmpty(biome))
                return true;

            foreach (string keyword in RSM_BiomeRegistry.ExcludedKeywords)
            {
                if (biome.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
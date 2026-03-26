using System;
using System.Collections;
using LivingPlanetSystem.RandomSpawnerModule;
using UnityEngine;
using WorldHeightLib;

namespace LivingPlanetSystem.RandomEventModule.Events
{
    /// <summary>
    /// Responsible for finding a valid underwater spawn position around the player
    /// for REM events that need to place a creature in the world.
    ///
    /// Strategy :
    ///   Sample random points inside a horizontal ring around the player
    ///   (between spawnRadiusMin and spawnRadiusMax), with a random downward
    ///   vertical offset from the player's Y, then validate each candidate
    ///   against three conditions :
    ///     1. Y strictly below 0 (underwater).
    ///     2. Y strictly above groundHeight + GroundClearance (WorldHeightLib).
    ///     3. Biome not in RSM_BiomeRegistry.ExcludedKeywords.
    ///   Retries up to MaxAttempts times before giving up.
    ///
    /// Search radius and vertical offset are passed per call so each event
    /// can independently control where its creature is allowed to spawn.
    /// </summary>
    public static class REM_SpawnLocator
    {
        // Validation constants

        /// Minimum clearance in metres above the terrain.
        private const float GroundClearance = 10f;

        /// Maximum number of candidate positions tested before giving up.
        private const int MaxAttempts = 20;

        // Public API

        /// <summary>
        /// Searches for a valid spawn position in a horizontal ring around the player.
        public static IEnumerator Find(Action<Vector3?> onCompleted,
            float spawnRadiusMin = 150f,
            float spawnRadiusMax = 400f,
            float verticalOffsetMax = 65f)
        {
            // Guard : player must be present
            GameObject player = Player.main?.gameObject;
            if (player == null)
            {
                Plugin.Log.LogWarning("[REM_SpawnLocator] Player not found : aborting.");
                onCompleted?.Invoke(null);
                yield break;
            }

            // Guard : WorldHeightLib must be available
            if (HeightMap.Instance == null)
            {
                Plugin.Log.LogWarning("[REM_SpawnLocator] HeightMap.Instance is null : aborting.");
                onCompleted?.Invoke(null);
                yield break;
            }

            Vector3 playerPos = player.transform.position;

            Plugin.Log.LogInfo($"[REM_SpawnLocator] Searching for spawn position " +
                               $"(radiusMin={spawnRadiusMin}, radiusMax={spawnRadiusMax}, " +
                               $"verticalOffsetMax={verticalOffsetMax})...");

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized;
                float distance = UnityEngine.Random.Range(spawnRadiusMin, spawnRadiusMax);
                float verticalOffset = UnityEngine.Random.Range(0f, verticalOffsetMax);

                Vector3 candidate = new Vector3(
                    playerPos.x + randomCircle.x * distance,
                    playerPos.y - verticalOffset,
                    playerPos.z + randomCircle.y * distance
                );

                if (IsPositionValid(candidate))
                {
                    Plugin.Log.LogInfo($"[REM_SpawnLocator] Valid position found on attempt {attempt} : {candidate}.");
                    onCompleted?.Invoke(candidate);
                    yield break;
                }

                Plugin.Log.LogDebug($"[REM_SpawnLocator] Attempt {attempt}/{MaxAttempts} invalid : {candidate}.");
                yield return null;
            }

            Plugin.Log.LogWarning($"[REM_SpawnLocator] No valid position found after {MaxAttempts} attempts.");
            onCompleted?.Invoke(null);
        }

        // Private helpers

        private static bool IsPositionValid(Vector3 position)
        {
            // 1. Must be underwater
            if (position.y >= 0f)
                return false;

            // 2. Must be above terrain with clearance
            if (!HeightMap.Instance.TryGetValueAtPosition(
                    new Vector2(position.x, position.z), out float groundHeight))
            {
                Plugin.Log.LogDebug($"[REM_SpawnLocator] HeightMap returned no data for " +
                                    $"({position.x:F0}, {position.z:F0}) : rejecting.");
                return false;
            }

            if (position.y <= groundHeight + GroundClearance)
            {
                Plugin.Log.LogDebug($"[REM_SpawnLocator] y={position.y:F1} too close to ground " +
                                    $"(ground={groundHeight:F1}, " +
                                    $"required above {groundHeight + GroundClearance:F1}) : rejecting.");
                return false;
            }

            // 3. Biome must not be excluded
            string biome = WaterBiomeManager.main?.GetBiome(position) ?? string.Empty;

            if (IsBiomeExcluded(biome))
            {
                Plugin.Log.LogDebug($"[REM_SpawnLocator] Biome '{biome}' at {position} is excluded : rejecting.");
                return false;
            }

            return true;
        }

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
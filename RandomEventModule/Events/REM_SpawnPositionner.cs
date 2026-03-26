using System;
using System.Collections;
using LivingPlanetSystem.RandomSpawnerModule;
using UnityEngine;
using WorldHeightLib;

namespace LivingPlanetSystem.RandomEventModule.Events
{
    /// <summary>
    /// Responsible for finding two valid world positions for events that require a migration path visible to the player.
    ///
    /// Strategy :
    ///   1. Pick a random horizontal direction D around the player.
    ///   2. Compute a lateral perpendicular offset so the swarm passes near the player without going exactly through them.
    ///   3. Apply a random upward vertical offset to avoid generating positions at exactly the player's depth, which is often at or near the terrain.
    ///   4. Spawn  A = playerPos - D * SpawnDistance + lateralOffset + verticalOffset
    ///      Dest   B = playerPos + D * SpawnDistance + lateralOffset + verticalOffset
    ///
    ///   This guarantees the segment AB passes within OffsetMax metres of the player : Always inside the player's field of vision radius.
    ///
    /// A position is valid if :
    ///   - y is strictly below 0 (Underwater)
    ///   - y is strictly above groundHeight + GroundClearance (Using WorldHeightLib)
    ///   - its biome is not excluded (reuses RSM_BiomeRegistry exclusion logic)
    ///
    /// </summary>
    public static class REM_SpawnPositioner
    {
        // Constants

        /// Distance from the player to the spawn and destination points along direction D.
        private const float SpawnDistance = 200f;
        private const float DestinationDistance = 250f; 

        /// Maximum lateral offset applied to both points so the path varies.
        private const float OffsetMax = 20f;

        /// Maximum upward vertical offset applied to both points.
        private const float VerticalOffsetMax = 60f;

        /// Minimum clearance in metres above the terrain required for a position to be valid.
        private const float GroundClearance = 10f;

        /// Maximum number of attempts to find valid positions.
        private const int MaxAttempts = 10;

        // Public data structure

        /// Holds the two positions required to run a migration event.
        public class MigrationPath
        {
            public Vector3 SpawnPosition { get; set; }
            public Vector3 DestinationPosition { get; set; }
        }

        // Public API

        /// Attempts to find a valid spawn and destination position guaranteeing the migration path passes within the player's field of vision.
        public static IEnumerator Find(Action<MigrationPath> onCompleted)
        {
            GameObject player = Player.main?.gameObject;

            if (player == null)
            {
                Plugin.Log.LogWarning("[REM_SpawnPositioner] Player not found : aborting.");
                onCompleted?.Invoke(null);
                yield break;
            }

            if (LargeWorld.main == null)
            {
                Plugin.Log.LogWarning("[REM_SpawnPositioner] LargeWorld.main is null : aborting.");
                onCompleted?.Invoke(null);
                yield break;
            }

            if (HeightMap.Instance == null)
            {
                Plugin.Log.LogWarning("[REM_SpawnPositioner] HeightMap.Instance is null : aborting.");
                onCompleted?.Invoke(null);
                yield break;
            }

            Vector3 playerPos = player.transform.position;
            MigrationPath path = null;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                // Step 1 : pick a random horizontal direction
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized;
                Vector3 direction = new Vector3(randomCircle.x, 0f, randomCircle.y);

                // Step 2 : compute a perpendicular lateral offset in the XZ plane
                Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x);
                float offsetAmount = UnityEngine.Random.Range(-OffsetMax, OffsetMax);
                Vector3 lateralOffset = perpendicular * offsetAmount;

                // Step 3 : random upward vertical offset
                float verticalAmount = UnityEngine.Random.Range(0f, VerticalOffsetMax);
                Vector3 verticalOffset = new Vector3(0f, verticalAmount, 0f);

                // Step 4 : derive spawn and destination
                Vector3 spawnPos = playerPos - direction * SpawnDistance + lateralOffset + verticalOffset;
                Vector3 destinationPos = playerPos + direction * DestinationDistance + lateralOffset + verticalOffset;

                // Step 5 : validate both positions
                if (!IsPositionValid(spawnPos))
                {
                    Plugin.Log.LogDebug($"[REM_SpawnPositioner] Attempt {attempt} : spawn position invalid.");
                    continue;
                }

                if (!IsPositionValid(destinationPos))
                {
                    Plugin.Log.LogDebug($"[REM_SpawnPositioner] Attempt {attempt} : destination position invalid.");
                    continue;
                }

                path = new MigrationPath
                {
                    SpawnPosition = spawnPos,
                    DestinationPosition = destinationPos
                };

                Plugin.Log.LogInfo($"[REM_SpawnPositioner] Valid path found on attempt {attempt}.");
                Plugin.Log.LogInfo($"[REM_SpawnPositioner] Spawn       : {spawnPos}");
                Plugin.Log.LogInfo($"[REM_SpawnPositioner] Destination : {destinationPos}");
                Plugin.Log.LogInfo($"[REM_SpawnPositioner] Lateral offset : {offsetAmount:F1}m — " +
                                   $"Vertical offset : {verticalAmount:F1}m");
                break;
            }

            if (path == null)
            {
                Plugin.Log.LogWarning($"[REM_SpawnPositioner] No valid path found after {MaxAttempts} attempts : aborting.");
            }

            onCompleted?.Invoke(path);
        }

        // Private helpers

        /// Returns true if the position is underwater, above the terrain, and in a non-excluded biome.
        private static bool IsPositionValid(Vector3 position)
        {
            // Must be underwater
            if (position.y >= 0f)
                return false;

            // Must be above terrain with clearance : requires WorldHeightLib
            float groundHeight;
            if (!HeightMap.Instance.TryGetValueAtPosition(
                    new Vector2(position.x, position.z), out groundHeight))
            {
                Plugin.Log.LogDebug($"[REM_SpawnPositioner] HeightMap returned no data for " +
                                    $"({position.x:F0}, {position.z:F0}) : rejecting.");
                return false;
            }

            if (position.y <= groundHeight + GroundClearance)
            {
                Plugin.Log.LogDebug($"[REM_SpawnPositioner] Position y={position.y:F1} too close to ground " +
                                    $"(groundHeight={groundHeight:F1}, " +
                                    $"required={groundHeight + GroundClearance:F1}) : rejecting.");
                return false;
            }

            // Biome must not be excluded
            string biome = LargeWorld.main.GetBiome(position);

            if (IsBiomeExcluded(biome))
                return false;

            return true;
        }

        /// Returns true if the biome name contains any keyword from RSM_BiomeRegistry.ExcludedKeywords.
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
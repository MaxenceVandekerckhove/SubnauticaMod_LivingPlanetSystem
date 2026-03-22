using System;
using System.Collections;
using System.Collections.Generic;
using LivingPlanetSystem.RandomSpawnerModule;
using UnityEngine;

namespace LivingPlanetSystem.RandomEventModule.Events
{
    /// <summary>
    /// Responsible for finding a valid spawn position for REM events.
    /// 
    /// Strategy : scan all active Creature instances in the scene and use one of them
    /// as a spawn anchor. This guarantees the position is in water, in a loaded chunk,
    /// and in a naturally inhabited area.
    /// 
    /// A candidate creature is valid if :
    ///   - Its distance to the player is greater than SpawnMinDistance.
    ///   - Its Y position is greater than or equal to the player's Y position.
    ///   - Its biome name does not contain any keyword from RSM_BiomeRegistry.ExcludedKeywords.
    /// 
    /// </summary>
    public static class REM_SpawnLocator
    {
        // Constants

        /// Minimum distance from the player for a spawn candidate to be valid.
        public const float SpawnMinDistance = 200f;

        /// Maximum distance from the player for a spawn candidate to be valid.
        public const float SpawnMaxDistance = 600f;

        /// Seconds to wait between retry attempts.
        private const float RetryDelay = 5f;

        /// Maximum number of scan attempts before giving up.
        private const int MaxRetries = 5;

        // Public API

        /// Scans active creatures in the scene to find a valid spawn position.
        public static IEnumerator Find(Action<Vector3?> onCompleted)
        {
            GameObject player = Player.main?.gameObject;

            if (player == null)
            {
                Plugin.Log.LogWarning("[REM_SpawnLocator] Player not found : aborting.");
                onCompleted?.Invoke(null);
                yield break;
            }

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                Plugin.Log.LogInfo($"[REM_SpawnLocator] Scan attempt {attempt}/{MaxRetries}...");

                Vector3? result = ScanCreatures(player);

                if (result.HasValue)
                {
                    Plugin.Log.LogInfo($"[REM_SpawnLocator] Valid position found : {result.Value}.");
                    onCompleted?.Invoke(result.Value);
                    yield break;
                }

                Plugin.Log.LogWarning($"[REM_SpawnLocator] No valid candidate on attempt {attempt}." +
                                      (attempt < MaxRetries ? $" Retrying in {RetryDelay}s..." : " Giving up."));

                if (attempt < MaxRetries)
                {
                    float waited = 0f;
                    while (waited < RetryDelay)
                    {
                        waited += Time.deltaTime;
                        yield return null;
                    }
                }
            }

            onCompleted?.Invoke(null);
        }

        // Private helpers

        /// Scans all active Creature instances, logs their data, filters valid candidates, and returns the position of a randomly chosen one.
        private static Vector3? ScanCreatures(GameObject player)
        {
            Creature[] allCreatures = UnityEngine.Object.FindObjectsOfType<Creature>();
            Vector3 playerPos = player.transform.position;
            var candidates = new List<Creature>();

            Plugin.Log.LogInfo($"[REM_SpawnLocator] {allCreatures.Length} creature(s) found in scene.");

            foreach (Creature creature in allCreatures)
            {
                if (creature == null || creature.gameObject == null)
                    continue;

                Vector3 pos = creature.transform.position;
                float distance = Vector3.Distance(pos, playerPos);
                string biomeName = WaterBiomeManager.main?.GetBiome(pos) ?? string.Empty;

                // Filter 1 : distance
                if (distance <= SpawnMinDistance || distance > SpawnMaxDistance)
                    continue;

                // Filter 2 : Y must be >= player Y
                if (pos.y < playerPos.y || pos.y >= 0f)
                    continue;

                // Filter 3 : biome must not contain any excluded keyword
                if (IsBiomeExcluded(biomeName))
                    continue;

                candidates.Add(creature);
            }

            Plugin.Log.LogInfo($"[REM_SpawnLocator] {candidates.Count} valid candidate(s) after filtering.");

            if (candidates.Count == 0)
                return null;

            int index = new System.Random().Next(candidates.Count);
            Vector3 chosen = candidates[index].transform.position;

            Plugin.Log.LogInfo($"[REM_SpawnLocator] Chosen candidate : {candidates[index].name} at {chosen}.");

            return chosen;
        }

        /// Returns true if the biome name contains any keyword from RSM_BiomeRegistry.ExcludedKeywords.
        private static bool IsBiomeExcluded(string biomeName)
        {
            if (string.IsNullOrEmpty(biomeName))
                return true;

            foreach (string keyword in RSM_BiomeRegistry.ExcludedKeywords)
            {
                if (biomeName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using LivingPlanetSystem.RandomSpawnerModule;
using UnityEngine;

namespace LivingPlanetSystem.RandomEventModule.Events.ApexPredatorHunt
{
    /// <summary>
    /// Responsible for building the pool of eligible apex predators for the ApexPredatorHunt event.
    /// 
    /// A creature is eligible if :
    ///   - It is classified as Large or LargeByName by RSM_SpawnManager.
    ///   - Its name does not match any keyword in the internal blacklist.
    ///   - Its prefab contains at least one AggressiveWhenSeeTarget or
    ///     AggressiveToPilotingVehicle component.
    /// 
    /// </summary>
    public static class REM_PredatorPool
    {
        // Internal blacklist
        private static readonly string[] Blacklist =
        {
            "baby"
        };

        // Public API

        // Reads the RSM creature cache, filters by size and blacklist, then checks each remaining candidate's prefab for an aggressive component.
        public static IEnumerator Build(Action<List<TechType>> onCompleted)
        {
            var cache = RSM_CreatureCache.LoadCache();
            var pool = new List<TechType>();

            int checkedCount = 0;
            int skippedSize = 0;
            int skippedBlacklist = 0;
            int skippedNoAggro = 0;

            foreach (var (techType, magnitude) in cache)
            {
                // 1. Size filter
                bool isLarge = RSM_SpawnManager.IsLargeByName(techType) ||
                               RSM_SpawnManager.IsLargeCategory(magnitude);

                if (!isLarge)
                {
                    skippedSize++;
                    continue;
                }

                // 2. Blacklist filter
                if (IsBlacklisted(techType))
                {
                    Plugin.Log.LogDebug($"[REM_PredatorPool] {techType} blacklisted : skipping.");
                    skippedBlacklist++;
                    continue;
                }

                checkedCount++;

                // 3. Aggro check
                var task = CraftData.GetPrefabForTechTypeAsync(techType, verbose: false);
                yield return task;

                GameObject prefab = task.GetResult();

                if (prefab == null)
                {
                    Plugin.Log.LogWarning($"[REM_PredatorPool] Could not load prefab for {techType} : skipping.");
                    skippedNoAggro++;
                    continue;
                }

                bool hasAggro =
                    prefab.GetComponentInChildren<AggressiveWhenSeeTarget>(includeInactive: true) != null ||
                    prefab.GetComponentInChildren<AggressiveToPilotingVehicle>(includeInactive: true) != null;

                if (!hasAggro)
                {
                    Plugin.Log.LogDebug($"[REM_PredatorPool] {techType} has no aggressive component : skipping.");
                    skippedNoAggro++;
                    continue;
                }

                pool.Add(techType);
            }

            Plugin.Log.LogInfo($"[REM_PredatorPool] Pool built : {pool.Count} eligible predator(s) " +
                               $"(checked={checkedCount}, skippedSize={skippedSize}, " +
                               $"skippedBlacklist={skippedBlacklist}, skippedNoAggro={skippedNoAggro}).");

            onCompleted?.Invoke(pool);
        }

        // Private helpers

        // Returns true if the creature name contains any blacklist keyword.
        private static bool IsBlacklisted(TechType techType)
        {
            string name = techType.ToString().ToLower();

            foreach (string keyword in Blacklist)
            {
                if (name.Contains(keyword))
                    return true;
            }

            return false;
        }
    }
}
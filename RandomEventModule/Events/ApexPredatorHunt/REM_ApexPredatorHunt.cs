using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LivingPlanetSystem.RandomEventModule.Events.ApexPredatorHunt
{
    /// <summary>
    /// Entry point for the ApexPredatorHunt random event.
    /// Extends REM_EventBase and orchestrates the full event sequence :
    ///
    ///   1. Build the eligible predator pool via REM_PredatorPool.
    ///   2. Pick a random predator from that pool.
    ///   3. Find a valid spawn position via REM_SpawnLocator.
    ///   4. Load and instantiate the predator prefab at that position.
    ///   5. Hand off to REM_HuntLoop to drive the hunt until an end condition is met.
    ///
    /// </summary>
    public class REM_ApexPredatorHunt : REM_EventBase
    {
        // REM_EventBase implementation

        public override string EventId => "ApexPredatorHunt";
        public override bool IsEnabled => LPS_Config.ApexPredatorHuntEnabled;
        public override float Weight => LPS_Config.ApexPredatorHuntWeight;

        // Execute

        protected override IEnumerator Execute()
        {
            Plugin.Log.LogInfo("[REM_ApexPredatorHunt] Building predator pool...");

            // 1. Build predator pool
            List<TechType> pool = null;
            yield return REM_PredatorPool.Build(result => pool = result);

            if (pool == null || pool.Count == 0)
            {
                Plugin.Log.LogWarning("[REM_ApexPredatorHunt] Predator pool is empty : aborting event.");
                yield break;
            }

            // 2. Pick a random predator
            TechType chosen = pool[new System.Random().Next(pool.Count)];
            Plugin.Log.LogInfo($"[REM_ApexPredatorHunt] Chosen predator : {chosen}.");

            // 3. Find a valid spawn position
            Vector3? spawnPos = null;
            yield return REM_SpawnLocator.Find(
                onCompleted: result => spawnPos = result,
                spawnRadiusMin: 300f,
                spawnRadiusMax: 600f,
                verticalOffsetMax: 65f
            );

            // 4. Load and instantiate prefab
            var task = CraftData.GetPrefabForTechTypeAsync(chosen, verbose: false);
            yield return task;

            GameObject prefab = task.GetResult();

            if (prefab == null)
            {
                Plugin.Log.LogWarning($"[REM_ApexPredatorHunt] Could not load prefab for {chosen} : aborting event.");
                yield break;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, spawnPos.Value, Quaternion.identity);
            instance.SetActive(true);

            yield return null;

            if (instance == null)
            {
                Plugin.Log.LogWarning("[REM_ApexPredatorHunt] Instance destroyed immediately after spawn : aborting event.");
                yield break;
            }

            Plugin.Log.LogInfo($"[REM_ApexPredatorHunt] {chosen} spawned at {spawnPos.Value}.");

            // 5. Hand off to hunt loop
            yield return REM_HuntLoop.Run(instance);
        }
    }
}
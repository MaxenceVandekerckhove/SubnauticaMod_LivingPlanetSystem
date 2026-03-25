using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LivingPlanetSystem.RandomEventModule.Events;

namespace LivingPlanetSystem.RandomEventModule.Events.Migration
{
    /// <summary>
    /// Entry point for migration events.
    ///
    /// Sequence for each event :
    ///   1. REM_SwarmPool : Pick a creature and build the SwarmComposition.
    ///   2. REM_SpawnPositioner : Find a spawn position and a destination that guarantee the migration path passes through the player's field of vision.
    ///   3. Spawn N adult instances and M juvenile instances around the spawn position.
    ///   4. REM_SwarmLoop : Drive all instances toward the destination, then release survivors to vanilla AI.
    ///   
    /// </summary>
    public abstract class REM_MigrationSwarm : REM_EventBase
    {
        // Abstract contract

        /// The size category handled by this migration event.
        protected abstract REM_MigrationCategory Category { get; }

        // Constants

        /// Radius within which individual instances are scattered around the spawn position.
        private const float SpawnScatterRadius = 40f;

        // Execute

        protected override IEnumerator Execute()
        {
            Plugin.Log.LogInfo($"[REM_MigrationSwarm] Starting {Category} migration...");

            // Step 1 : build swarm composition
            REM_SwarmPool.SwarmComposition composition = REM_SwarmPool.Build(Category, new System.Random());

            if (composition == null)
            {
                Plugin.Log.LogWarning($"[REM_MigrationSwarm] No eligible creature for {Category} : aborting.");
                yield break;
            }

            // Step 2 : find migration path
            REM_SpawnPositioner.MigrationPath path = null;
            yield return REM_SpawnPositioner.Find(result => path = result);

            if (path == null)
            {
                Plugin.Log.LogWarning($"[REM_MigrationSwarm] Could not find a valid migration path : aborting.");
                yield break;
            }

            // Step 3 : load prefab
            var task = CraftData.GetPrefabForTechTypeAsync(composition.TechType, verbose: false);
            yield return task;

            GameObject prefab = task.GetResult();

            if (prefab == null)
            {
                Plugin.Log.LogWarning($"[REM_MigrationSwarm] Could not load prefab for {composition.TechType} : aborting.");
                yield break;
            }

            // Step 4 : spawn adults and juveniles scattered around spawn position
            var adults = new List<GameObject>();
            var juveniles = new List<GameObject>();

            for (int i = 0; i < composition.AdultCount; i++)
            {
                GameObject instance = SpawnInstance(prefab, path.SpawnPosition);
                if (instance != null)
                    adults.Add(instance);
            }

            for (int i = 0; i < composition.JuvenileCount; i++)
            {
                GameObject instance = SpawnInstance(prefab, path.SpawnPosition);
                if (instance != null)
                    juveniles.Add(instance);
            }

            if (adults.Count == 0 && juveniles.Count == 0)
            {
                Plugin.Log.LogWarning($"[REM_MigrationSwarm] All instances failed to spawn : aborting.");
                yield break;
            }

            Plugin.Log.LogInfo($"[REM_MigrationSwarm] Spawned {adults.Count} adult(s) and " +
                               $"{juveniles.Count} juvenile(s) of {composition.TechType}.");

            // Wait one frame for all components to initialize
            yield return null;

            // Step 5 : hand off to swarm loop — pass Category for velocity selection
            yield return REM_SwarmLoop.Run(adults, juveniles, path.DestinationPosition, Category);

            Plugin.Log.LogInfo($"[REM_MigrationSwarm] {Category} migration complete.");
        }

        // Private helpers

        /// Instantiates one instance of the prefab at a random position scattered within SpawnScatterRadius around the spawn anchor.
        private static GameObject SpawnInstance(GameObject prefab, Vector3 spawnAnchor)
        {
            Vector2 scatter = Random.insideUnitCircle * SpawnScatterRadius;
            Vector3 spawnPos = spawnAnchor + new Vector3(scatter.x, 0f, scatter.y);
            GameObject instance = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
            instance.SetActive(true);

            return instance;
        }
    }

    // -------------------------------------------------------------------------
    // Concrete subclasses — one per migration category
    // -------------------------------------------------------------------------

    // Migration event for small creatures.
    public class REM_MigrationSmall : REM_MigrationSwarm
    {
        public override string EventId => "MigrationSmall";
        public override bool IsEnabled => LPS_Config.MigrationSmallEnabled;
        public override float Weight => LPS_Config.MigrationSmallWeight;

        protected override REM_MigrationCategory Category => REM_MigrationCategory.Small;
    }

    // Migration event for medium creatures.
    public class REM_MigrationMedium : REM_MigrationSwarm
    {
        public override string EventId => "MigrationMedium";
        public override bool IsEnabled => LPS_Config.MigrationMediumEnabled;
        public override float Weight => LPS_Config.MigrationMediumWeight;

        protected override REM_MigrationCategory Category => REM_MigrationCategory.Medium;
    }

    // Migration event for large creatures.
    public class REM_MigrationLarge : REM_MigrationSwarm
    {
        public override string EventId => "MigrationLarge";
        public override bool IsEnabled => LPS_Config.MigrationLargeEnabled;
        public override float Weight => LPS_Config.MigrationLargeWeight;

        protected override REM_MigrationCategory Category => REM_MigrationCategory.Large;
    }
}
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
    ///   2. REM_PathFinder : Find a valid migration path (A → B) that passes within the player's field of vision.
    ///   3. Spawn N adult instances and M juvenile instances around position A.
    ///   4. REM_SwarmLoop : Drive all instances toward B, then release survivors to vanilla AI.
    ///
    /// </summary>
    public abstract class REM_MigrationSwarm : REM_EventBase
    {
        // Abstract contract

        // The size category handled by this migration event.
        protected abstract REM_MigrationCategory Category { get; }

        // Path finder parameters
        protected virtual float DistanceA => 300f;
        protected virtual float DistanceB => 350f;
        protected virtual float MinPathLength => 200f;
        protected virtual float PlayerPassRadius => 20f;

        // Constants

        // Spawn scatter radius per migration category
        private const float SpawnScatterRadiusSmall = 5f;
        private const float SpawnScatterRadiusMedium = 15f;
        private const float SpawnScatterRadiusLarge = 20f;

        private const float SpawnScatterHeightSmall = 2f;
        private const float SpawnScatterHeightMedium = 8f;
        private const float SpawnScatterHeightLarge = 10f;

        // Execute

        protected override IEnumerator Execute()
        {
            Plugin.Log.LogInfo($"[REM_MigrationSwarm] Starting {Category} migration...");

            // 1. Build swarm composition
            REM_SwarmPool.SwarmComposition composition = REM_SwarmPool.Build(Category, new System.Random());

            if (composition == null)
            {
                Plugin.Log.LogWarning($"[REM_MigrationSwarm] No eligible creature for {Category} : aborting.");
                yield break;
            }

            // 2. Find migration path
            REM_PathFinder.MigrationPath path = null;
            yield return REM_PathFinder.Find(
                onCompleted: result => path = result,
                distanceA: DistanceA,
                distanceB: DistanceB,
                minPathLength: MinPathLength,
                playerPassRadius: PlayerPassRadius
            );

            if (path == null)
            {
                Plugin.Log.LogWarning($"[REM_MigrationSwarm] Could not find a valid migration path : aborting.");
                yield break;
            }

            // 3. Load prefab
            var task = CraftData.GetPrefabForTechTypeAsync(composition.TechType, verbose: false);
            yield return task;

            GameObject prefab = task.GetResult();

            if (prefab == null)
            {
                Plugin.Log.LogWarning($"[REM_MigrationSwarm] Could not load prefab for {composition.TechType} : aborting.");
                yield break;
            }

            // 4. Spawn adults and juveniles scattered around spawn position
            var (scatterRadius, scatterHeight) = GetScatterRadius(Category);

            var adults = new List<GameObject>();
            var juveniles = new List<GameObject>();

            for (int i = 0; i < composition.AdultCount; i++)
            {
                GameObject instance = SpawnInstance(prefab, path.SpawnPosition, scatterRadius, scatterHeight);
                if (instance != null)
                    adults.Add(instance);
            }

            for (int i = 0; i < composition.JuvenileCount; i++)
            {
                GameObject instance = SpawnInstance(prefab, path.SpawnPosition, scatterRadius, scatterHeight);
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

            yield return null;

            // 5. Hand off to swarm loop
            yield return REM_SwarmLoop.Run(adults, juveniles, path.DestinationPosition, Category);

            Plugin.Log.LogInfo($"[REM_MigrationSwarm] {Category} migration complete.");
        }

        // Private helpers

        // Returns the scatter radius appropriate for the migration category.
        private static (float horizontal, float vertical) GetScatterRadius(REM_MigrationCategory category)
        {
            switch (category)
            {
                case REM_MigrationCategory.Large: return (SpawnScatterRadiusLarge, SpawnScatterHeightLarge);
                case REM_MigrationCategory.Medium: return (SpawnScatterRadiusMedium, SpawnScatterHeightMedium);
                default: return (SpawnScatterRadiusSmall, SpawnScatterHeightSmall);
            }
        }

        // Instantiates one instance of the prefab at a random position scattered within scatterRadius around the spawn anchor.
        private static GameObject SpawnInstance(GameObject prefab, Vector3 spawnAnchor,
                                                float scatterRadius, float scatterHeight)
        {
            Vector2 scatter = Random.insideUnitCircle * scatterRadius;
            float offsetY = Random.Range(-scatterHeight, scatterHeight);
            Vector3 spawnPos = spawnAnchor + new Vector3(scatter.x, offsetY, scatter.y);

            GameObject instance = Object.Instantiate(prefab, spawnPos, Quaternion.identity);

            // Force a high enough cell level so the entity stays active at spawn distances beyond the Near/Medium streaming range.
            LargeWorldEntity lwe = instance.GetComponent<LargeWorldEntity>();
            if (lwe != null)
                lwe.cellLevel = LargeWorldEntity.CellLevel.VeryFar;

            LargeWorldEntity.Register(instance);

            instance.SetActive(true);
            return instance;
        }
    }

    // Concrete subclasses — one per migration category

    public class REM_MigrationSmall : REM_MigrationSwarm
    {
        public override string EventId => "MigrationSmall";
        public override bool IsEnabled => LPS_Config.MigrationSmallEnabled;
        public override float Weight => LPS_Config.MigrationSmallWeight;

        protected override REM_MigrationCategory Category => REM_MigrationCategory.Small;

        protected override float DistanceA => 130f;
        protected override float DistanceB => 150f;
    }

    public class REM_MigrationMedium : REM_MigrationSwarm
    {
        public override string EventId => "MigrationMedium";
        public override bool IsEnabled => LPS_Config.MigrationMediumEnabled;
        public override float Weight => LPS_Config.MigrationMediumWeight;

        protected override REM_MigrationCategory Category => REM_MigrationCategory.Medium;

        protected override float DistanceA => 220f;
        protected override float DistanceB => 250f;
    }

    public class REM_MigrationLarge : REM_MigrationSwarm
    {
        public override string EventId => "MigrationLarge";
        public override bool IsEnabled => LPS_Config.MigrationLargeEnabled;
        public override float Weight => LPS_Config.MigrationLargeWeight;

        protected override REM_MigrationCategory Category => REM_MigrationCategory.Large;

        protected override float DistanceA => 300f;
        protected override float DistanceB => 400f;
    }
}
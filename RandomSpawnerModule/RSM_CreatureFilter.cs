using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UWE;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    public static class RSM_CreatureFilter
    {
        public const float SIZE_LIMIT = 150f;

        private static bool isFiltering = false;


        private static readonly HashSet<string> ExcludedNameKeywords = new HashSet<string>()
        {
            "test",
            "example",
            "gargantuan",
            "nothing",
            "cutefish",
            "skyray",
            "seaemperor",
            "mrteeth",
            "consciousneuralmatter",
            "meatball",
            "gilbert"
        };

        public static List<TechType> AvailableCreatures { get; private set; } = new List<TechType>();

        public static void Initialize()
        {
            Plugin.Log.LogInfo("[RSM_CreatureFilter] Waiting for CreatureRegistry...");
            RSM_CreatureRegistry.OnCreaturesLoaded += StartFiltering;
        }

        private static void StartFiltering()
        {
            if (isFiltering)
            {
                Plugin.Log.LogWarning("[RSM_CreatureFilter] Filtering already running — skipping.");
                return;
            }

            Plugin.Log.LogInfo("[RSM_CreatureFilter] Starting creature filtering...");

            isFiltering = true;
            CoroutineHost.StartCoroutine(FilterCreatures());
        }

        private static IEnumerator FilterCreatures()
        {
            AvailableCreatures.Clear();
            var allCreatures = RSM_CreatureRegistry.GetAllCreatures();

            foreach (var techType in allCreatures)
            {
                var task = CraftData.GetPrefabForTechTypeAsync(techType, verbose: false);
                yield return task;

                GameObject prefab = task.GetResult();
                if (prefab == null)
                    continue;

                Creature creature = prefab.GetComponent<Creature>();
                if (creature == null)
                    continue;

                // SIZE CHECK
                float size = GetCreatureSize(prefab);

                if (size > SIZE_LIMIT)
                {
                    Plugin.Log.LogDebug($"[Filter] {techType} rejected (too large) size={size}");
                    continue;
                }

                // NAME EXCLUSION CHECK
                string name = techType.ToString().ToLower();

                if (IsNameExcluded(name))
                {
                    Plugin.Log.LogDebug($"[Filter] {techType} rejected by name exclusion rule");
                    continue;
                }

                AvailableCreatures.Add(techType);
            }

            Plugin.Log.LogInfo($"[RSM_CreatureFilter] Filtering complete : {AvailableCreatures.Count} creatures kept");

            if (AvailableCreatures.Count > 0)
                Plugin.Log.LogInfo($"[RSM_CreatureFilter] Final list: {string.Join(", ", AvailableCreatures)}");

            isFiltering = false;
        }

        private static bool IsNameExcluded(string name)
        {
            foreach (var keyword in ExcludedNameKeywords)
            {
                if (name.Contains(keyword))
                    return true;
            }
            return false;
        }

        private static float GetCreatureSize(GameObject prefab)
        {
            Renderer renderer = prefab.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                return 0f;
            }

            return renderer.bounds.size.magnitude;
        }

        // Clear AvailableCreatures list
        public static void Clear()
        {
            Plugin.Log.LogInfo("[RSM_CreatureFilter] Clearing filtered creature list.");
            AvailableCreatures.Clear();
            isFiltering = false;
        }
    }
}

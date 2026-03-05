using System;
using System.Collections.Generic;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Responsible for filtering the raw creature list produced by RSM_CreatureRegistry.
    /// Removes creatures that are unsuitable for random spawning based on name exclusion rules.
    /// The size filter from the previous version has been removed — bounds on uninstantiated
    /// prefabs are unreliable. Exclusion is handled by name keywords instead.
    /// Filtered results are stored and passed to RSM_CreatureCache for persistence.
    /// </summary>
    public static class RSM_CreatureFilter
    {
        // Private state

        private static List<TechType> filteredCreatures = new List<TechType>();

        // Exclusion rules

        /// Any creature whose TechType name contains one of these strings will be excluded.
        private static readonly string[] ExcludedKeywords =
        {
            "test",
            "example",
            "gargantuan",
            "cutefish",
            "skyray",
            "seaemperor",
            "mrteeth",
            "consciousneuralmatter",
            "meatball",
            "gilbert"
        };

        // Public API

        /// Filters the raw creature list and stores the result.
        public static void Filter(List<TechType> rawCreatures)
        {
            filteredCreatures.Clear();

            int totalInput = rawCreatures.Count;
            int totalExcluded = 0;

            foreach (TechType techType in rawCreatures)
            {
                string name = techType.ToString().ToLower();

                if (IsNameExcluded(name))
                {
                    Plugin.Log.LogDebug($"[RSM_CreatureFilter] {techType} excluded by name rule.");
                    totalExcluded++;
                    continue;
                }

                filteredCreatures.Add(techType);
            }

            Plugin.Log.LogInfo($"[RSM_CreatureFilter] Filtering complete : " +
                               $"{filteredCreatures.Count} creatures kept, " +
                               $"{totalExcluded} excluded out of {totalInput} total.");

            Plugin.Log.LogInfo($"[RSM_CreatureFilter] Final list : {string.Join(", ", filteredCreatures)}");
        }

        /// Returns a copy of the filtered creature list.
        public static List<TechType> GetFilteredCreatures()
        {
            return new List<TechType>(filteredCreatures);
        }

        /// Returns the number of available creatures after filtering.
        public static int Count => filteredCreatures.Count;

        /// Clears the filtered creature list.
        public static void Clear()
        {
            Plugin.Log.LogInfo("[RSM_CreatureFilter] Clearing filtered creature list.");
            filteredCreatures.Clear();
        }

        // Private helpers

        /// Returns true if the creature name contains any excluded keyword.
        private static bool IsNameExcluded(string name)
        {
            foreach (string keyword in ExcludedKeywords)
            {
                if (name.Contains(keyword))
                    return true;
            }
            return false;
        }
    }
}
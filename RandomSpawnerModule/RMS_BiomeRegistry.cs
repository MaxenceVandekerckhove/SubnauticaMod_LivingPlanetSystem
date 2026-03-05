using System;
using System.Collections.Generic;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Responsible for registering all valid biomes from the game.
    /// Filters out biomes that are not suitable for creature spawning
    /// (interiors, technical zones, unused biomes, etc.).
    /// </summary>
    public static class RSM_BiomeRegistry
    {
        // Private state

        private static readonly HashSet<BiomeType> registeredBiomes = new HashSet<BiomeType>();

        // Exclusion rules

        /// Any biome whose name contains one of these strings will be excluded.
        private static readonly string[] ExcludedKeywords =
        {
            "Unassigned",
            "Obsolete",
            "Unused",
            "TechSite",
            "Techsite",
            "EscapePod",
            "AbandonedBase",
            "CrabSnake",
            "InsideShroom",
            "FloatingIslands",
            "Castle",
            "ShipSpecial",
            "ShipInterior",
            "Medkit",
            "LostRiverBase",
            "ThermalVent",
            "PrisonAquarium",
            "Precursor",
            "Fragment",
            "CrashHome",
            "Mountains_Island",
            "Aurora",
            "Supply",
            "Birds",
            "Void",
        };

        // Public API

        /// Scans all BiomeType enum values and registers the ones that are valid for creature spawning.
        public static void Initialize()
        {
            if (registeredBiomes.Count > 0)
            {
                Plugin.Log.LogWarning("[RSM_BiomeRegistry] Already initialized : skipping.");
                return;
            }

            int totalScanned = 0;
            int totalExcluded = 0;

            foreach (BiomeType biome in Enum.GetValues(typeof(BiomeType)))
            {
                totalScanned++;
                string biomeName = biome.ToString();

                if (IsExcluded(biomeName))
                {
                    totalExcluded++;
                    continue;
                }

                registeredBiomes.Add(biome);
            }

            Plugin.Log.LogInfo($"[RSM_BiomeRegistry] Initialization complete : " +
                               $"{registeredBiomes.Count} biomes registered, " +
                               $"{totalExcluded} excluded out of {totalScanned} total.");
        }

        /// Returns a copy of all registered valid biomes.
        public static List<BiomeType> GetAllBiomes()
        {
            return new List<BiomeType>(registeredBiomes);
        }

        /// Returns the number of registered biomes.
        public static int Count => registeredBiomes.Count;

        /// Clears all registered biomes.
        public static void Clear()
        {
            Plugin.Log.LogInfo("[RSM_BiomeRegistry] Clearing biome registry.");
            registeredBiomes.Clear();
        }

        // Private helpers

        /// Returns true if the biome name contains any excluded keyword.
        private static bool IsExcluded(string biomeName)
        {
            foreach (string keyword in ExcludedKeywords)
            {
                if (biomeName.Contains(keyword))
                    return true;
            }
            return false;
        }
    }
}
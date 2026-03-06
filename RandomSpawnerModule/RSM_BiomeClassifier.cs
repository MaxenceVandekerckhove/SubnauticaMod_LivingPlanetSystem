using System.Collections.Generic;
using LivingPlanetSystem.Core;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Responsible for classifying biomes into two categories :
    ///   - Unrestricted : accessible to all creatures regardless of size
    ///   - Restricted   : forbidden for large creatures (magnitude >= 20)
    /// Classification is based on keyword matching on biome names.
    /// Large cave systems are explicitly whitelisted to allow large creatures inside them.
    /// </summary>
    public static class RSM_BiomeClassifier
    {
        // Private state

        private static readonly List<BiomeType> unrestrictedBiomes = new List<BiomeType>();
        private static readonly List<BiomeType> restrictedBiomes = new List<BiomeType>();

        // Size threshold

        /// Creatures with a collider magnitude above this value are considered large and will be forbidden from spawning in restricted biomes.
        public const float LargeCreatureMagnitudeThreshold = 20f;

        // Restriction keywords

        /// Biome name keywords that make a biome forbidden for large creatures.
        /// Whitelist keywords always take priority over these.
        private static readonly string[] RestrictedKeywords =
        {
            "SafeShallows",
            "CaveFloor",
            "CaveWall",
            "CaveCeiling",
            "CaveSand",
            "CaveSpecial",
            "CaveEntrance",
            "CaveRecess",
            "CavePlants",
            "IslandCave",
            "ShellTunnel",
            "OpenShallow",
            "GiantTreeInterior",
            "Cave"
        };

        /// Biome name keywords that override restriction rules.
        private static readonly string[] WhitelistKeywords =
        {
            "JellyShroomCaves",
            "SkeletonCave",
            "LostRiver",
            "TreeCove",
            "BonesField",
            "GhostTree",
            "ActiveLavaZone",
            "InactiveLavaZone",
        };

        // Public API

        /// Classifies all registered biomes into unrestricted and restricted lists.
        public static void Initialize()
        {
            unrestrictedBiomes.Clear();
            restrictedBiomes.Clear();

            foreach (BiomeType biome in RSM_BiomeRegistry.GetAllBiomes())
            {
                if (IsRestricted(biome))
                {
                    restrictedBiomes.Add(biome);
                }
                else
                {
                    unrestrictedBiomes.Add(biome);
                }
            }

            Plugin.Log.LogInfo($"[RSM_BiomeClassifier] Classification complete : " +
                               $"{unrestrictedBiomes.Count} unrestricted, " +
                               $"{restrictedBiomes.Count} restricted.");
        }

        /// Returns the appropriate biome list for a creature based on its magnitude.
        public static List<BiomeType> GetEligibleBiomes(float magnitude)
        {
            if (magnitude >= LargeCreatureMagnitudeThreshold)
            {
                Plugin.Log.LogDebug($"[RSM_BiomeClassifier] Large creature (magnitude={magnitude:F2}) " +
                                    $"— {unrestrictedBiomes.Count} eligible biomes.");
                return new List<BiomeType>(unrestrictedBiomes);
            }

            Plugin.Log.LogDebug($"[RSM_BiomeClassifier] Small creature (magnitude={magnitude:F2}) " +
                                $"— all {RSM_BiomeRegistry.Count} biomes eligible.");
            return RSM_BiomeRegistry.GetAllBiomes();
        }

        /// Returns the number of unrestricted biomes.
        public static int UnrestrictedCount => unrestrictedBiomes.Count;

        /// Returns the number of restricted biomes.
        public static int RestrictedCount => restrictedBiomes.Count;

        /// Clears all classified biome lists.
        public static void Clear()
        {
            Plugin.Log.LogInfo("[RSM_BiomeClassifier] Clearing biome classifier.");
            unrestrictedBiomes.Clear();
            restrictedBiomes.Clear();
        }

        // Private helpers

        /// Returns true if the biome should be forbidden for large creatures.
        private static bool IsRestricted(BiomeType biome)
        {
            string name = biome.ToString();

            // Whitelist check first — large cave systems allow big creatures
            foreach (string whitelist in WhitelistKeywords)
            {
                if (name.IndexOf(whitelist, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            // Restriction keyword check
            foreach (string keyword in RestrictedKeywords)
            {
                if (name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
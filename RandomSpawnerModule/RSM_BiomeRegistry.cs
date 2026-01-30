using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    public static class RSM_BiomeRegistry
    {
        // List of all the biomes in the game
        private static HashSet<BiomeType> biomes = new HashSet<BiomeType>();

        private static readonly string[] ExcludedKeywords = new string[]
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
            "void",
        };

        public static void RegisterAllBiomes()
        {
            foreach (BiomeType biome in Enum.GetValues(typeof(BiomeType)))
            {
                string biomeName = biome.ToString();

                bool exclude = false;
                foreach (string keyword in ExcludedKeywords)
                {
                    if (biomeName.Contains(keyword))
                    {
                        exclude = true;
                        break;
                    }
                }

                if (exclude) continue;

                biomes.Add(biome);
            }

            Plugin.Log.LogInfo($"[RSM_BiomeRegistry] Total biomes registered: {biomes.Count}");
        }

        public static IEnumerable<BiomeType> GetAllBiomes()
        {
            return biomes;
        }

        // Clear biomes list
        public static void Clear()
        {
            Plugin.Log.LogInfo("[RSM_BiomeRegistry] Clearing biome registry.");
            biomes.Clear();
        }
    }
}

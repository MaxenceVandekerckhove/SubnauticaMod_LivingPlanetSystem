using System;
using System.IO;
using BepInEx;
using LivingPlanetSystem.Core;

namespace LivingPlanetSystem
{
    /// <summary>
    /// Manages the random seed used for spawn distribution generation.
    /// The seed is persisted per world slot to ensure consistent distributions
    /// across sessions. A new seed is generated only on first world creation.
    /// 
    /// Behavior :
    ///   - First load of a slot  → new seed generated and saved to disk
    ///   - Subsequent loads      → seed reloaded from disk
    ///   - Slot deleted          → orphan seed file cleaned up automatically
    /// </summary>
    public static class LPS_SeedManager
    {
        // Public state

        public static int CurrentSeed { get; private set; }
        public static Random Random { get; private set; }

        // Private state

        private static readonly string SeedDirectory = Path.Combine(
            Paths.BepInExRootPath, "plugins", "LivingPlanetSystem", "seeds");

        // Public API

        /// Initializes the seed for the current world slot.
        /// Loads an existing seed if available, otherwise generates and persists a new one.
        public static void InitializeForCurrentSlot()
        {
            string slot = SaveLoadManager.main.GetCurrentSlot();
            string path = GetSeedPath(slot);

            if (File.Exists(path))
            {
                string raw = File.ReadAllText(path).Trim();

                if (int.TryParse(raw, out int saved))
                {
                    CurrentSeed = saved;
                    Random = new Random(CurrentSeed);
                    Plugin.Log.LogInfo($"[LPS_SeedManager] Seed loaded for slot '{slot}' : {CurrentSeed}");
                    return;
                }

                Plugin.Log.LogWarning($"[LPS_SeedManager] Corrupt seed file for slot '{slot}' — regenerating.");
            }

            GenerateAndSave(slot, path);
        }

        /// Deletes the seed file for a given slot.
        public static void DeleteSeedForSlot(string slot)
        {
            string path = GetSeedPath(slot);

            if (!File.Exists(path))
                return;

            File.Delete(path);
            Plugin.Log.LogInfo($"[LPS_SeedManager] Seed deleted for slot '{slot}'.");
        }

        /// Deletes orphan seed files that no longer have a matching save slot.
        public static void CleanOrphanSeeds()
        {
            if (!Directory.Exists(SeedDirectory))
                return;

            string[] validSlots = SaveLoadManager.main.GetActiveSlotNames();
            string[] seedFiles = Directory.GetFiles(SeedDirectory, "*.seed");

            foreach (string file in seedFiles)
            {
                string slot = Path.GetFileNameWithoutExtension(file);

                if (Array.IndexOf(validSlots, slot) < 0)
                {
                    File.Delete(file);
                    Plugin.Log.LogInfo($"[LPS_SeedManager] Orphan seed deleted for slot '{slot}'.");
                }
            }
        }

        // Private helpers

        private static void GenerateAndSave(string slot, string path)
        {
            Directory.CreateDirectory(SeedDirectory);

            CurrentSeed = new Random().Next(int.MinValue, int.MaxValue);
            Random = new Random(CurrentSeed);

            File.WriteAllText(path, CurrentSeed.ToString());
            Plugin.Log.LogInfo($"[LPS_SeedManager] New seed generated for slot '{slot}' : {CurrentSeed}");
        }

        private static string GetSeedPath(string slot)
        {
            return Path.Combine(SeedDirectory, $"{slot}.seed");
        }
    }
}
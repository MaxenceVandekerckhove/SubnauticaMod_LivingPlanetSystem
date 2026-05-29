using BepInEx;
using LivingPlanetSystem.Core;
using LivingPlanetSystem.RandomSpawnerModule;
using Newtonsoft.Json;
using System;
using System.IO;

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

        // Serialized data structure

        private class SlotData
        {
            public int Seed { get; set; }

            // Whether the safe zone center has been captured for this slot.
            // False on first load or after a legacy migration : the center will be captured from Player.main during the current session.
            public bool SafeZoneCaptured { get; set; } = false;

            public float SafeZoneX { get; set; } = 0f;
            public float SafeZoneZ { get; set; } = 0f;
        }

        // Public API

        // Initializes the seed and safe zone for the current world slot.
        // Loads existing data if available, handles legacy migration, or generates fresh data on first load.
        public static void InitializeForCurrentSlot()
        {
            string slot = SaveLoadManager.main.GetCurrentSlot();
            string jsonPath = GetJsonPath(slot);
            string legacyPath = GetLegacyPath(slot);

            // Legacy migration : .seed file exists but no .json yet
            if (!File.Exists(jsonPath) && File.Exists(legacyPath))
            {
                MigrateLegacyFile(slot, legacyPath, jsonPath);
            }

            if (File.Exists(jsonPath))
            {
                try
                {
                    SlotData data = ReadJsonFile(jsonPath);

                    CurrentSeed = data.Seed;
                    Random = new Random(CurrentSeed);

                    Plugin.Log.LogInfo($"[LPS_SeedManager] Slot '{slot}' loaded : seed={CurrentSeed}.");

                    if (data.SafeZoneCaptured)
                    {
                        RSM_SafeZone.Initialize(new UnityEngine.Vector3(data.SafeZoneX, 0f, data.SafeZoneZ));
                        Plugin.Log.LogInfo($"[LPS_SeedManager] Safe zone loaded for slot '{slot}' : " +
                                           $"({data.SafeZoneX:F0}, {data.SafeZoneZ:F0}).");
                    }
                    else
                    {
                        // Center was not yet captured (migration case or first load edge case)
                        // Capture now from Player.main and persist
                        CaptureAndSaveSafeZone(slot, jsonPath, data);
                    }

                    return;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[LPS_SeedManager] Corrupt JSON for slot '{slot}' : {e.Message} — regenerating.");
                }
            }

            // No file at all : generate everything from scratch
            GenerateAndSave(slot, jsonPath);
        }

        // Deletes the seed file for a given slot.
        public static void DeleteSeedForSlot(string slot)
        {
            string jsonPath = GetJsonPath(slot);
            string legacyPath = GetLegacyPath(slot);

            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
                Plugin.Log.LogInfo($"[LPS_SeedManager] JSON file deleted for slot '{slot}'.");
            }

            // Clean up any leftover legacy file as well
            if (File.Exists(legacyPath))
            {
                File.Delete(legacyPath);
                Plugin.Log.LogInfo($"[LPS_SeedManager] Legacy .seed file deleted for slot '{slot}'.");
            }
        }

        // Deletes orphan seed files that no longer have a matching save slot.
        public static void CleanOrphanSeeds()
        {
            if (!Directory.Exists(SeedDirectory))
                return;

            string[] validSlots = SaveLoadManager.main.GetActiveSlotNames();

            foreach (string file in Directory.GetFiles(SeedDirectory, "*.json"))
            {
                string slot = Path.GetFileNameWithoutExtension(file);
                if (Array.IndexOf(validSlots, slot) < 0)
                {
                    File.Delete(file);
                    Plugin.Log.LogInfo($"[LPS_SeedManager] Orphan JSON deleted for slot '{slot}'.");
                }
            }

            foreach (string file in Directory.GetFiles(SeedDirectory, "*.seed"))
            {
                string slot = Path.GetFileNameWithoutExtension(file);
                if (Array.IndexOf(validSlots, slot) < 0)
                {
                    File.Delete(file);
                    Plugin.Log.LogInfo($"[LPS_SeedManager] Orphan .seed deleted for slot '{slot}'.");
                }
            }
        }

        // Private helpers

        private static void GenerateAndSave(string slot, string jsonPath)
        {
            Directory.CreateDirectory(SeedDirectory);

            CurrentSeed = new Random().Next(int.MinValue, int.MaxValue);
            Random = new Random(CurrentSeed);

            SlotData data = new SlotData { Seed = CurrentSeed };
            CaptureAndSaveSafeZone(slot, jsonPath, data);

            Plugin.Log.LogInfo($"[LPS_SeedManager] New seed generated for slot '{slot}' : {CurrentSeed}.");
        }

        // Captures the safe zone center from Player.main, updates the SlotData, and writes to disk.
        // If Player.main is unavailable the safe zone is left uncaptured and will be retried next load.
        private static void CaptureAndSaveSafeZone(string slot, string jsonPath, SlotData data)
        {
            if (Player.main != null)
            {
                UnityEngine.Vector3 pos = Player.main.transform.position;
                data.SafeZoneX = pos.x;
                data.SafeZoneZ = pos.z;
                data.SafeZoneCaptured = true;

                RSM_SafeZone.Initialize(new UnityEngine.Vector3(pos.x, 0f, pos.z));

                Plugin.Log.LogInfo($"[LPS_SeedManager] Safe zone captured for slot '{slot}' : " +
                                   $"({pos.x:F0}, {pos.z:F0}).");
            }
            else
            {
                Plugin.Log.LogWarning($"[LPS_SeedManager] Player.main is null : " +
                                      $"safe zone not captured for slot '{slot}'. Will retry next load.");
            }

            WriteJsonFile(jsonPath, data);
        }

        // Reads the legacy raw-integer .seed file, preserves the seed value, writes a new JSON file, and deletes the old .seed file.
        private static void MigrateLegacyFile(string slot, string legacyPath, string jsonPath)
        {
            try
            {
                string raw = File.ReadAllText(legacyPath).Trim();

                if (!int.TryParse(raw, out int legacySeed))
                {
                    Plugin.Log.LogWarning($"[LPS_SeedManager] Could not parse legacy seed for '{slot}' : discarding.");
                    File.Delete(legacyPath);
                    return;
                }

                // Write JSON with the recovered seed ; safe zone will be captured this session
                Directory.CreateDirectory(SeedDirectory);
                WriteJsonFile(jsonPath, new SlotData { Seed = legacySeed, SafeZoneCaptured = false });

                File.Delete(legacyPath);

                Plugin.Log.LogInfo($"[LPS_SeedManager] Migrated legacy .seed for slot '{slot}' " +
                                   $"(seed={legacySeed}) → JSON format.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[LPS_SeedManager] Migration failed for slot '{slot}' : {e.Message}");
            }
        }

        // Reads the SlotData from a JSON file.
        private static SlotData ReadJsonFile(string path)
        {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<SlotData>(json) ?? new SlotData();
        }

        private static void WriteJsonFile(string path, SlotData data)
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        // Constructs the expected JSON file path for a given slot.
        private static string GetJsonPath(string slot) =>
            Path.Combine(SeedDirectory, $"{slot}.json");
        
        // Constructs the expected legacy .seed file path for a given slot.
        private static string GetLegacyPath(string slot) =>
            Path.Combine(SeedDirectory, $"{slot}.seed");
    }
}
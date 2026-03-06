using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Responsible for reading and writing the creature cache to disk.
    /// The cache stores the list of valid creatures with their magnitude and a mod fingerprint.
    /// If the fingerprint changes (mods added/removed/updated), the cache is invalidated and a new scan will be triggered automatically.
    /// </summary>
    public static class RSM_CreatureCache
    {
        // Constants

        private static readonly string CacheFilePath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "creature_cache.json"
        );

        // Cache data structures

        /// Represents a single creature entry in the cache.
        public class CreatureEntry
        {
            // TechType name of the creature
            public string TechType { get; set; } = string.Empty;

            // Collider magnitude measured during the filter phase
            public float Magnitude { get; set; } = 0f;
        }

        /// Full cache data structure stored in the JSON file.
        private class CacheData
        {
            // Mod fingerprint : pipe-separated list of "GUID@version" for all active plugins
            public string ModsFingerprint { get; set; } = string.Empty;

            // List of creature entries that passed the scan and filter phases
            public List<CreatureEntry> Creatures { get; set; } = new List<CreatureEntry>();
        }

        // Public API

        /// Checks whether a valid cache exists for the current mod configuration.
        public static bool IsCacheValid()
        {
            if (!File.Exists(CacheFilePath))
            {
                Plugin.Log.LogInfo("[RSM_CreatureCache] No cache file found : cache is invalid.");
                return false;
            }

            try
            {
                CacheData data = ReadCacheFile();
                string currentFingerprint = BuildFingerprint();

                if (data.ModsFingerprint != currentFingerprint)
                {
                    Plugin.Log.LogInfo("[RSM_CreatureCache] Fingerprint mismatch : cache is invalid.");
                    Plugin.Log.LogInfo($"[RSM_CreatureCache] Saved   : {data.ModsFingerprint}");
                    Plugin.Log.LogInfo($"[RSM_CreatureCache] Current : {currentFingerprint}");
                    return false;
                }

                Plugin.Log.LogInfo($"[RSM_CreatureCache] Cache is valid : {data.Creatures.Count} creatures loaded.");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[RSM_CreatureCache] Failed to read cache file : {e.Message}");
                return false;
            }
        }

        /// Saves the given list of creatures and their magnitudes to the cache file.
        public static void SaveCache(List<(TechType techType, float magnitude)> creatures)
        {
            try
            {
                List<CreatureEntry> entries = new List<CreatureEntry>();
                foreach (var (techType, magnitude) in creatures)
                {
                    entries.Add(new CreatureEntry
                    {
                        TechType = techType.ToString(),
                        Magnitude = magnitude
                    });
                }

                CacheData data = new CacheData
                {
                    ModsFingerprint = BuildFingerprint(),
                    Creatures = entries
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(CacheFilePath, json);

                Plugin.Log.LogInfo($"[RSM_CreatureCache] Cache saved : {entries.Count} creatures, " +
                                   $"fingerprint : {data.ModsFingerprint}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[RSM_CreatureCache] Failed to save cache file : {e.Message}");
            }
        }

        /// Loads the creature list from the cache file.
        /// Returns an empty list if the file cannot be read.
        public static List<(TechType techType, float magnitude)> LoadCache()
        {
            var result = new List<(TechType techType, float magnitude)>();

            try
            {
                CacheData data = ReadCacheFile();

                foreach (CreatureEntry entry in data.Creatures)
                {
                    if (TechTypeExtensions.FromString(entry.TechType, out TechType techType, false))
                    {
                        result.Add((techType, entry.Magnitude));
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"[RSM_CreatureCache] Could not parse TechType : " +
                                              $"'{entry.TechType}' — skipping.");
                    }
                }

                Plugin.Log.LogInfo($"[RSM_CreatureCache] Cache loaded : {result.Count} creatures.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[RSM_CreatureCache] Failed to load cache : {e.Message}");
            }

            return result;
        }

        // Private helpers

        /// Reads and deserializes the cache file from disk.
        private static CacheData ReadCacheFile()
        {
            string json = File.ReadAllText(CacheFilePath);
            return JsonConvert.DeserializeObject<CacheData>(json) ?? new CacheData();
        }

        /// Builds a fingerprint string based on all currently loaded BepInEx plugins.
        /// Includes both GUID and version so the cache is invalidated on mod updates.
        private static string BuildFingerprint()
        {
            List<string> entries = new List<string>();

            foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
                entries.Add($"{plugin.Metadata.GUID}@{plugin.Metadata.Version}");

            entries.Sort();
            return string.Join("|", entries);
        }
    }
}
using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Responsible for reading and writing the creature cache to disk.
    /// The cache stores the list of valid creatures and a mod fingerprint.
    /// If the fingerprint changes (mods added/removed), the cache is invalidated
    /// and a new scan will be triggered automatically.
    /// </summary>
    public static class RSM_CreatureCache
    {
        // Constants

        // Cache file will be stored next to the mod DLL
        private static readonly string CacheFilePath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "creature_cache.json"
        );

        // Cache data structure
        private class CacheData
        {
            // Mod fingerprint : pipe-separated list of active BepInEx plugin GUIDs
            public string ModsFingerprint { get; set; } = string.Empty;

            // List of TechType names that passed the creature scan and filter
            public List<string> Creatures { get; set; } = new List<string>();
        }

        // Public API
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
                    Plugin.Log.LogInfo($"[RSM_CreatureCache] Saved    : {data.ModsFingerprint}");
                    Plugin.Log.LogInfo($"[RSM_CreatureCache] Current  : {currentFingerprint}");
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

        /// Saves the given list of TechTypes to the cache file along with the current fingerprint.
        public static void SaveCache(List<TechType> creatures)
        {
            try
            {
                // Convert TechType list to string list for JSON serialization
                List<string> creatureNames = new List<string>();
                foreach (TechType techType in creatures)
                    creatureNames.Add(techType.ToString());

                CacheData data = new CacheData
                {
                    ModsFingerprint = BuildFingerprint(),
                    Creatures = creatureNames
                };

                string json = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(CacheFilePath, json);

                Plugin.Log.LogInfo($"[RSM_CreatureCache] Cache saved : {creatures.Count} creatures, fingerprint : {data.ModsFingerprint}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[RSM_CreatureCache] Failed to save cache file : {e.Message}");
            }
        }

        /// <summary>
        /// Loads the creature list from the cache file.
        /// Returns an empty list if the file cannot be read.
        /// Call IsCacheValid() before calling this method.
        /// </summary>
        public static List<TechType> LoadCache()
        {
            List<TechType> result = new List<TechType>();

            try
            {
                CacheData data = ReadCacheFile();

                foreach (string name in data.Creatures)
                {
                    // Try to parse each string back into a TechType enum value
                    if (Enum.TryParse(name, out TechType techType))
                    {
                        result.Add(techType);
                    }
                    else
                    {
                        // This can happen if a mod that added a creature is removed
                        Plugin.Log.LogWarning($"[RSM_CreatureCache] Could not parse TechType : '{name}' : skipping.");
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
        /// The fingerprint changes whenever a mod is added or removed.
        private static string BuildFingerprint()
        {
            List<string> entries = new List<string>();

            foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                // Format : "com.example.modname@1.2.3"
                string entry = $"{plugin.Metadata.GUID}@{plugin.Metadata.Version}";
                entries.Add(entry);
            }

            // Sort to ensure consistent order regardless of load order
            entries.Sort();

            return string.Join("|", entries);
        }
    }
}
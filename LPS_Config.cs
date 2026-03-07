using BepInEx.Configuration;
using Nautilus.Options;
using Nautilus.Handlers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace LivingPlanetSystem
{
    /// <summary>
    /// Global configuration for the LivingPlanetSystem mod.
    /// Settings are registered in Subnautica's in-game Mods menu via Nautilus.
    /// The creature blacklist is managed via a dedicated blacklist.txt file.
    /// </summary>
    public static class LPS_Config
    {
        // Sections

        private const string SectionSpawn = "Spawn";

        // Paths

        private static readonly string BlacklistPath = Path.Combine(
            Paths.BepInExRootPath, "plugins", "LivingPlanetSystem", "blacklist.txt");

        // Default excluded keywords

        private static readonly string[] DefaultExcludedKeywords =
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
            "gilbert",
            "silence",
            "dragonfly",
            "bloom"
        };

        // Config entries

        private static ConfigEntry<float> spawnMultiplier;
        private static string[] cachedKeywords;

        // Nested ModOptions

        private class LPS_ModOptions : ModOptions
        {
            public LPS_ModOptions() : base("Living Planet System")
            {
                var spawnSlider = ModSliderOption.Create(
                    id: "SpawnMultiplier",
                    label: "Spawn Multiplier",
                    minValue: 0.1f,
                    maxValue: 10.0f,
                    value: spawnMultiplier.Value,
                    defaultValue: 1.0f,
                    step: 0.1f,
                    valueFormat: "{0:F1}"
                );

                spawnSlider.OnChanged += (_, args) =>
                {
                    spawnMultiplier.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] SpawnMultiplier updated : {spawnMultiplier.Value}");
                };

                AddItem(spawnSlider);
            }
        }

        // Public API

        /// Initializes all configuration entries and registers them in Subnautica's in-game Mods menu via Nautilus.
        /// Also initializes the blacklist from blacklist.txt, creating it with defaults if it doesn't exist.
        public static void Initialize(ConfigFile config)
        {
            spawnMultiplier = config.Bind(
                section: SectionSpawn,
                key: "SpawnMultiplier",
                defaultValue: 1.0f,
                description: "Global spawn rate multiplier applied to all creatures. " +
                             "1.0 = default rates | 0.1 = very rare | 10.0 = very frequent. " +
                             "Acceptable range: 0.1 to 10.0"
            );

            OptionsPanelHandler.RegisterModOptions(new LPS_ModOptions());

            InitializeBlacklist();

            Plugin.Log.LogInfo($"[LPS_Config] Configuration loaded : SpawnMultiplier={SpawnMultiplier}");
            Plugin.Log.LogInfo($"[LPS_Config] Blacklist loaded : {cachedKeywords.Length} keywords : " +
                               $"{string.Join(", ", cachedKeywords)}");
        }

        /// Returns the current spawn multiplier value set by the player.
        public static float SpawnMultiplier => spawnMultiplier.Value;

        /// Returns the current excluded keywords loaded from blacklist.txt.
        public static string[] ExcludedKeywords => cachedKeywords;

        /// Returns a normalized string representation of the current keywords.
        public static string ExcludedKeywordsFingerprint =>
            string.Join(",", cachedKeywords.OrderBy(k => k));

        // Private helpers

        /// Loads the blacklist from blacklist.txt.
        private static void InitializeBlacklist()
        {
            if (!File.Exists(BlacklistPath))
                CreateDefaultBlacklist();

            cachedKeywords = ReadBlacklist();
        }

        /// Creates the blacklist.txt file with default keywords and usage instructions.
        private static void CreateDefaultBlacklist()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BlacklistPath));

            var lines = new List<string>
            {
                "# Living Planet System — Creature Blacklist",
                "# One keyword per line. Any creature whose name contains a keyword will be excluded from spawning.",
                "# Lines starting with # are comments and are ignored.",
                "# Changes take effect after restarting the game or reloading the world.",
                "#",
                "# Examples :",
                "#   warper        → excludes any creature whose name contains 'warper'",
                "#   leviathan     → excludes any creature whose name contains 'leviathans'",
                ""
            };

            lines.AddRange(DefaultExcludedKeywords);

            File.WriteAllLines(BlacklistPath, lines);
            Plugin.Log.LogInfo($"[LPS_Config] blacklist.txt created with default keywords at : {BlacklistPath}");
        }

        /// Reads and parses the blacklist.txt file, ignoring comments and empty lines.
        private static string[] ReadBlacklist()
        {
            return File.ReadAllLines(BlacklistPath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                .Select(line => line.ToLower())
                .ToArray();
        }
    }
}
using BepInEx.Configuration;
using Nautilus.Options;
using Nautilus.Handlers;
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
    /// Each module has its own ModOptions entry in the in-game menu.
    /// </summary>
    public static class LPS_Config
    {
        // Sections

        private const string SectionSpawn = "Spawn";
        private const string SectionSizeVariation = "SizeVariation";

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
            "bloom",
            "DeepBloop",
            "ACU",
            "LostOculusJuvenile",
            "VoidMouth",
            "MultiGarg"
        };

        // Config entries

        private static ConfigEntry<float> spawnMultiplier;
        private static ConfigEntry<bool> sizeVariationEnabled;
        private static ConfigEntry<float> sizeVariationMin;
        private static ConfigEntry<float> sizeVariationMax;
        private static string[] cachedKeywords;

        // Nested ModOptions

        /// Core LPS options — Spawn Multiplier
        private class LPS_CoreOptions : ModOptions
        {
            public LPS_CoreOptions() : base("Living Planet System - Random Spawner Module")
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

        /// SVM options — Size Variation Module
        private class SVM_ModOptions : ModOptions
        {
            public SVM_ModOptions() : base("Living Planet System — Size Variation Module")
            {
                var toggle = ModToggleOption.Create(
                    id: "SizeVariationEnabled",
                    label: "Enable Size Variation",
                    value: sizeVariationEnabled.Value
                );

                toggle.OnChanged += (_, args) =>
                {
                    sizeVariationEnabled.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] SizeVariationEnabled updated : {sizeVariationEnabled.Value}");
                };

                AddItem(toggle);

                var minSlider = ModSliderOption.Create(
                    id: "SizeVariationMin",
                    label: "Size Variation Min",
                    minValue: 0.1f,
                    maxValue: 2.0f,
                    value: sizeVariationMin.Value,
                    defaultValue: 0.5f,
                    step: 0.1f,
                    valueFormat: "{0:F2}"
                );

                minSlider.OnChanged += (_, args) =>
                {
                    sizeVariationMin.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] SizeVariationMin updated : {sizeVariationMin.Value}");
                };

                AddItem(minSlider);

                var maxSlider = ModSliderOption.Create(
                    id: "SizeVariationMax",
                    label: "Size Variation Max",
                    minValue: 0.1f,
                    maxValue: 5.0f,
                    value: sizeVariationMax.Value,
                    defaultValue: 1.8f,
                    step: 0.1f,
                    valueFormat: "{0:F2}"
                );

                maxSlider.OnChanged += (_, args) =>
                {
                    sizeVariationMax.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] SizeVariationMax updated : {sizeVariationMax.Value}");
                };

                AddItem(maxSlider);
            }
        }

        // Public API

        /// Initializes all configuration entries and registers them in Subnautica's in-game Mods menu via Nautilus.
        /// Also initializes the blacklist from blacklist.txt, creating it with defaults if it doesn't exist.
        public static void Initialize(ConfigFile config)
        {
            // Spawn
            spawnMultiplier = config.Bind(
                section: SectionSpawn,
                key: "SpawnMultiplier",
                defaultValue: 1.0f,
                description: "Global spawn rate multiplier applied to all creatures. " +
                             "1.0 = default rates | 0.1 = very rare | 10.0 = very frequent. " +
                             "Acceptable range: 0.1 to 10.0"
            );

            // Size Variation
            sizeVariationEnabled = config.Bind(
                section: SectionSizeVariation,
                key: "SizeVariationEnabled",
                defaultValue: false,
                description: "Enable or disable the Size Variation module. " +
                             "When enabled, each creature spawns with a randomized scale."
            );

            sizeVariationMin = config.Bind(
                section: SectionSizeVariation,
                key: "SizeVariationMin",
                defaultValue: 0.5f,
                description: "Minimum scale multiplier applied to spawned creatures. " +
                             "0.5 = half size | 1.0 = normal size. Acceptable range: 0.1 to 2.0"
            );

            sizeVariationMax = config.Bind(
                section: SectionSizeVariation,
                key: "SizeVariationMax",
                defaultValue: 1.8f,
                description: "Maximum scale multiplier applied to spawned creatures. " +
                             "2.0 = double size | 3.0 = triple size. Acceptable range: 0.1 to 3.0"
            );

            OptionsPanelHandler.RegisterModOptions(new LPS_CoreOptions());
            OptionsPanelHandler.RegisterModOptions(new SVM_ModOptions());

            InitializeBlacklist();

            Plugin.Log.LogInfo($"[LPS_Config] Configuration loaded : SpawnMultiplier={SpawnMultiplier}");
            Plugin.Log.LogInfo($"[LPS_Config] SizeVariation : Enabled={SizeVariationEnabled} " +
                               $"Min={SizeVariationMin} Max={SizeVariationMax}");
            Plugin.Log.LogInfo($"[LPS_Config] Blacklist loaded : {cachedKeywords.Length} keywords : " +
                               $"{string.Join(", ", cachedKeywords)}");
        }

        // Public properties

        /// Returns the current spawn multiplier value.
        public static float SpawnMultiplier => spawnMultiplier.Value;

        /// Returns whether the Size Variation module is enabled.
        public static bool SizeVariationEnabled => sizeVariationEnabled.Value;

        /// Returns the minimum scale multiplier for size variation.
        public static float SizeVariationMin => sizeVariationMin.Value;

        /// Returns the maximum scale multiplier for size variation.
        public static float SizeVariationMax => sizeVariationMax.Value;

        /// Returns the current excluded keywords loaded from blacklist.txt.
        public static string[] ExcludedKeywords => cachedKeywords;

        /// Returns a normalized string representation of the current keywords.
        public static string ExcludedKeywordsFingerprint =>
            string.Join(",", cachedKeywords.OrderBy(k => k));

        // Private helpers

        private static void InitializeBlacklist()
        {
            if (!File.Exists(BlacklistPath))
                CreateDefaultBlacklist();

            cachedKeywords = ReadBlacklist();
        }

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
                "#   leviathan     → excludes any creature whose name contains 'leviathan'",
                ""
            };

            lines.AddRange(DefaultExcludedKeywords);

            File.WriteAllLines(BlacklistPath, lines);
            Plugin.Log.LogInfo($"[LPS_Config] blacklist.txt created with default keywords at : {BlacklistPath}");
        }

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
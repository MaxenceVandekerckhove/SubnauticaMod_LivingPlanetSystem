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
        private const string SectionRandomEvent = "RandomEvent";

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
            "MultiGarg",
            "school",
            "Rockgrub",
            "Kinematic",
            "Precursor",
            "Crash",
            "PodshellLeviathanBaby"
        };

        // Config entries — RSM

        private static ConfigEntry<float> spawnMultiplier;

        private static ConfigEntry<bool> safeZoneEnabled;
        private static ConfigEntry<float> safeZoneRadius;

        private static ConfigEntry<bool> debugScannerEnabled;
        public static bool DebugScannerEnabled => debugScannerEnabled.Value;


        // Config entries — SVM

        private static ConfigEntry<bool> sizeVariationEnabled;
        private static ConfigEntry<float> sizeVariationMin;
        private static ConfigEntry<float> sizeVariationMax;

        // Config entries — REM

        private static ConfigEntry<bool> randomEventEnabled;
        private static ConfigEntry<bool> pdaVoiceEnabled;
        private static ConfigEntry<bool> despawnAfterEvent;

        private static ConfigEntry<float> eventIntervalMin;
        private static ConfigEntry<float> eventIntervalMax;

        private static ConfigEntry<bool> apexPredatorHuntEnabled;
        private static ConfigEntry<float> apexPredatorHuntWeight;
        private static ConfigEntry<bool> migrationSmallEnabled;
        private static ConfigEntry<float> migrationSmallWeight;
        private static ConfigEntry<bool> migrationMediumEnabled;
        private static ConfigEntry<float> migrationMediumWeight;
        private static ConfigEntry<bool> migrationLargeEnabled;
        private static ConfigEntry<float> migrationLargeWeight;

        // Cached blacklist

        private static string[] cachedKeywords;

        // Nested ModOptions

        /// Core LPS options — Spawn Multiplier
        private class LPS_CoreOptions : ModOptions
        {
            public LPS_CoreOptions() : base("Living Planet System - 1. Random Spawner Module")
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

                // SafeZone toggle
                var safeZoneToggle = ModToggleOption.Create(
                    id: "SafeZoneEnabled",
                    label: "Enable Spawn Safe Zone",
                    value: safeZoneEnabled.Value
                );
                safeZoneToggle.OnChanged += (_, args) =>
                {
                    safeZoneEnabled.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] SafeZoneEnabled updated : {safeZoneEnabled.Value}");
                };
                AddItem(safeZoneToggle);

                // SafeZone radius slider
                var safeZoneSlider = ModSliderOption.Create(
                    id: "SafeZoneRadius",
                    label: "Safe Zone Radius (metres)",
                    minValue: 50f,
                    maxValue: 300f,
                    value: safeZoneRadius.Value,
                    defaultValue: 150f,
                    step: 10f,
                    valueFormat: "{0:F0} m"
                );
                safeZoneSlider.OnChanged += (_, args) =>
                {
                    safeZoneRadius.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] SafeZoneRadius updated : {safeZoneRadius.Value}");
                };
                AddItem(safeZoneSlider);
            }
        }

        /// SVM options — Size Variation Module
        private class SVM_ModOptions : ModOptions
        {
            public SVM_ModOptions() : base("Living Planet System — 2. Size Variation Module")
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

        /// REM options — Random Event Module
        private class REM_ModOptions : ModOptions
        {
            public REM_ModOptions() : base("Living Planet System — 3. Random Event Module")
            {
                // Master toggle
                var enableToggle = ModToggleOption.Create(
                    id: "RandomEventEnabled",
                    label: "Enable Random Events",
                    value: randomEventEnabled.Value
                );

                enableToggle.OnChanged += (_, args) =>
                {
                    randomEventEnabled.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] RandomEventEnabled updated : {randomEventEnabled.Value}");
                };

                AddItem(enableToggle);

                // Despawn after event toggle
                var despawnToggle = ModToggleOption.Create(
                    id: "DespawnAfterEvent",
                    label: "Despawn Creatures After Event",
                    value: despawnAfterEvent.Value
                );

                despawnToggle.OnChanged += (_, args) =>
                {
                    despawnAfterEvent.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] DespawnAfterEvent updated : {despawnAfterEvent.Value}");
                };

                AddItem(despawnToggle);

                // PDA voice toggle
                var pdaVoiceToggle = ModToggleOption.Create(
                    id: "PDAVoiceEnabled",
                    label: "Enable PDA Voice Alerts",
                    value: pdaVoiceEnabled.Value
                );

                pdaVoiceToggle.OnChanged += (_, args) =>
                {
                    pdaVoiceEnabled.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] PDAVoiceEnabled updated : {pdaVoiceEnabled.Value}");
                };

                AddItem(pdaVoiceToggle);

                // Interval min
                var intervalMinSlider = ModSliderOption.Create(
                    id: "EventIntervalMin",
                    label: "Event Interval Min (minutes)",
                    minValue: 5f,
                    maxValue: 120f,
                    value: eventIntervalMin.Value,
                    defaultValue: 35f,
                    step: 1f,
                    valueFormat: "{0:F0} min"
                );

                intervalMinSlider.OnChanged += (_, args) =>
                {
                    eventIntervalMin.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] EventIntervalMin updated : {eventIntervalMin.Value}");
                };

                AddItem(intervalMinSlider);

                // Interval max
                var intervalMaxSlider = ModSliderOption.Create(
                    id: "EventIntervalMax",
                    label: "Event Interval Max (minutes)",
                    minValue: 5f,
                    maxValue: 120f,
                    value: eventIntervalMax.Value,
                    defaultValue: 50f,
                    step: 1f,
                    valueFormat: "{0:F0} min"
                );

                intervalMaxSlider.OnChanged += (_, args) =>
                {
                    eventIntervalMax.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] EventIntervalMax updated : {eventIntervalMax.Value}");
                };

                AddItem(intervalMaxSlider);

                // ApexPredatorHunt toggle
                var apexToggle = ModToggleOption.Create(
                    id: "ApexPredatorHuntEnabled",
                    label: "Enable Apex Predator Hunt",
                    value: apexPredatorHuntEnabled.Value
                );

                apexToggle.OnChanged += (_, args) =>
                {
                    apexPredatorHuntEnabled.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] ApexPredatorHuntEnabled updated : {apexPredatorHuntEnabled.Value}");
                };

                AddItem(apexToggle);

                // ApexPredatorHunt weight
                var apexWeightSlider = ModSliderOption.Create(
                    id: "ApexPredatorHuntWeight",
                    label: "Apex Predator Hunt Weight",
                    minValue: 0.1f,
                    maxValue: 10f,
                    value: apexPredatorHuntWeight.Value,
                    defaultValue: 0.2f,
                    step: 0.1f,
                    valueFormat: "{0:F1}"
                );

                apexWeightSlider.OnChanged += (_, args) =>
                {
                    apexPredatorHuntWeight.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] ApexPredatorHuntWeight updated : {apexPredatorHuntWeight.Value}");
                };

                AddItem(apexWeightSlider);

                // Small migration toggle
                var smallToggle = ModToggleOption.Create(
                    id: "MigrationSmallEnabled",
                    label: "Enable Small Creature Migration",
                    value: migrationSmallEnabled.Value
                );

                smallToggle.OnChanged += (_, args) =>
                {
                    migrationSmallEnabled.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] MigrationSmallEnabled updated : {migrationSmallEnabled.Value}");
                };

                AddItem(smallToggle);

                // Small migration weight
                var smallWeightSlider = ModSliderOption.Create(
                    id: "MigrationSmallWeight",
                    label: "Small Migration Creature Weight",
                    minValue: 0.1f,
                    maxValue: 10f,
                    value: migrationSmallWeight.Value,
                    defaultValue: 0.3f,
                    step: 0.1f,
                    valueFormat: "{0:F1}"
                );

                smallWeightSlider.OnChanged += (_, args) =>
                {
                    migrationSmallWeight.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] MigrationSmallWeight updated : {migrationSmallWeight.Value}");
                };

                AddItem(smallWeightSlider);

                // Medium migration toggle
                var mediumToggle = ModToggleOption.Create(
                    id: "MigrationMediumEnabled",
                    label: "Enable Medium Creature Migration",
                    value: migrationMediumEnabled.Value
                );

                mediumToggle.OnChanged += (_, args) =>
                {
                    migrationMediumEnabled.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] MigrationMediumEnabled updated : {migrationMediumEnabled.Value}");
                };

                AddItem(mediumToggle);

                // Medium migration weight
                var mediumWeightSlider = ModSliderOption.Create(
                    id: "MigrationMediumWeight",
                    label: "Medium Migration Creature Weight",
                    minValue: 0.1f,
                    maxValue: 10f,
                    value: migrationMediumWeight.Value,
                    defaultValue: 0.4f,
                    step: 0.1f,
                    valueFormat: "{0:F1}"
                );

                mediumWeightSlider.OnChanged += (_, args) =>
                {
                    migrationMediumWeight.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] MigrationMediumWeight updated : {migrationMediumWeight.Value}");
                };

                AddItem(mediumWeightSlider);

                // Large migration toggle
                var largeToggle = ModToggleOption.Create(
                    id: "MigrationLargeEnabled",
                    label: "Enable Large Creature Migration",
                    value: migrationLargeEnabled.Value
                );

                largeToggle.OnChanged += (_, args) =>
                {
                    migrationLargeEnabled.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] MigrationLargeEnabled updated : {migrationLargeEnabled.Value}");
                };

                AddItem(largeToggle);

                // Large migration weight
                var largeWeightSlider = ModSliderOption.Create(
                    id: "MigrationLargeWeight",
                    label: "Large Migration Creature Weight",
                    minValue: 0.1f,
                    maxValue: 10f,
                    value: migrationLargeWeight.Value,
                    defaultValue: 0.1f,
                    step: 0.1f,
                    valueFormat: "{0:F1}"
                );

                largeWeightSlider.OnChanged += (_, args) =>
                {
                    migrationLargeWeight.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] MigrationLargeWeight updated : {migrationLargeWeight.Value}");
                };

                AddItem(largeWeightSlider);
            }
        }
        private class LPS_DebugOptions : ModOptions
        {
            public LPS_DebugOptions() : base("Living Planet System — 4. Debug")
            {
                var toggle = ModToggleOption.Create(
                    id: "DebugScannerEnabled",
                    label: "Enable Creature Scanner (F10)",
                    value: debugScannerEnabled.Value
                );

                toggle.OnChanged += (_, args) =>
                {
                    debugScannerEnabled.Value = args.Value;
                    Plugin.Log.LogInfo($"[LPS_Config] DebugScannerEnabled updated : {debugScannerEnabled.Value}");
                };

                AddItem(toggle);
            }
        }

        // Public API

        /// Initializes all configuration entries and registers them in Subnautica's in-game Mods menu via Nautilus.
        /// Also initializes the blacklist from blacklist.txt, creating it with defaults if it doesn't exist.
        public static void Initialize(ConfigFile config)
        {
            // RSM - Spawn
            spawnMultiplier = config.Bind(
                section: SectionSpawn,
                key: "SpawnMultiplier",
                defaultValue: 1.0f,
                description: "Global spawn rate multiplier applied to all creatures. " +
                             "1.0 = default rates | 0.1 = very rare | 10.0 = very frequent. " +
                             "Acceptable range: 0.1 to 10.0"
            );
            safeZoneEnabled = config.Bind(
                section: SectionSpawn,
                key: "SafeZoneEnabled",
                defaultValue: false,
                description: "When enabled, no creatures will be spawned by RSM within SafeZoneRadius " +
                             "metres of the player's initial spawn position."
            );
            safeZoneRadius = config.Bind(
                section: SectionSpawn,
                key: "SafeZoneRadius",
                defaultValue: 150f,
                description: "Radius in metres of the spawn-free zone around the player's spawn point. " +
                             "Acceptable range: 50 to 300."
            );
            debugScannerEnabled = config.Bind(
                section: "Debug",
                key: "DebugScannerEnabled",
                defaultValue: false,
                description: "When enabled, pressing F10 in-game will log the TechType and magnitude of the nearest creature."
            );

            // SVM - Size Variation
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

            // REM — Random Event
            randomEventEnabled = config.Bind(
                section: SectionRandomEvent,
                key: "RandomEventEnabled",
                defaultValue: false,
                description: "Enable or disable the Random Event module entirely."
            );

            pdaVoiceEnabled = config.Bind(
                section: SectionRandomEvent,
                key: "PDAVoiceEnabled",
                defaultValue: true,
                description: "Enable or disable the PDA voice sound played when random event occurs."
            );

            despawnAfterEvent = config.Bind(
                section: SectionRandomEvent,
                key: "DespawnAfterEvent",
                defaultValue: false,
                description: "If enabled, creatures spawned during a random event will despawn " +
                             "one minute after the event ends instead of being released to vanilla AI."
            );

            eventIntervalMin = config.Bind(
                section: SectionRandomEvent,
                key: "EventIntervalMin",
                defaultValue: 20f,
                description: "Minimum time in minutes between random events. Acceptable range: 5 to 120."
            );

            eventIntervalMax = config.Bind(
                section: SectionRandomEvent,
                key: "EventIntervalMax",
                defaultValue: 35f,
                description: "Maximum time in minutes between random events. Acceptable range: 5 to 120."
            );

            apexPredatorHuntEnabled = config.Bind(
                section: SectionRandomEvent,
                key: "ApexPredatorHuntEnabled",
                defaultValue: true,
                description: "Enable or disable the Apex Predator Hunt event specifically."
            );

            apexPredatorHuntWeight = config.Bind(
                section: SectionRandomEvent,
                key: "ApexPredatorHuntWeight",
                defaultValue: 0.2f,
                description: "Relative weight for the Apex Predator Hunt event during weighted random selection. " +
                             "Higher values make it more likely to be chosen when multiple events are available."
            );

            migrationSmallEnabled = config.Bind(
                section: SectionRandomEvent,
                key: "MigrationSmallEnabled",
                defaultValue: true,
                description: "Enable or disable the Small Migration event. " +
                             "Spawns a group of 15–20 adults and 5–12 juveniles of a small creature."
            );

            migrationSmallWeight = config.Bind(
                section: SectionRandomEvent,
                key: "MigrationSmallWeight",
                defaultValue: 0.3f,
                description: "Relative weight for the Small Migration event during weighted random selection."
            );

            migrationMediumEnabled = config.Bind(
                section: SectionRandomEvent,
                key: "MigrationMediumEnabled",
                defaultValue: true,
                description: "Enable or disable the Medium Migration event. " +
                             "Spawns a group of 10–15 adults and 2–8 juveniles of a medium creature."
            );

            migrationMediumWeight = config.Bind(
                section: SectionRandomEvent,
                key: "MigrationMediumWeight",
                defaultValue: 0.4f,
                description: "Relative weight for the Medium Migration event during weighted random selection."
            );

            migrationLargeEnabled = config.Bind(
                section: SectionRandomEvent,
                key: "MigrationLargeEnabled",
                defaultValue: true,
                description: "Enable or disable the Large Migration event. " +
                             "Spawns a group of 1–3 adults and 0–3 juveniles of a large creature."
            );

            migrationLargeWeight = config.Bind(
                section: SectionRandomEvent,
                key: "MigrationLargeWeight",
                defaultValue: 0.1f,
                description: "Relative weight for the Large Migration event during weighted random selection."
            );

            OptionsPanelHandler.RegisterModOptions(new LPS_CoreOptions());
            OptionsPanelHandler.RegisterModOptions(new SVM_ModOptions());
            OptionsPanelHandler.RegisterModOptions(new REM_ModOptions());
            OptionsPanelHandler.RegisterModOptions(new LPS_DebugOptions());

            InitializeBlacklist();

            Plugin.Log.LogInfo($"[LPS_Config] Configuration loaded : SpawnMultiplier={SpawnMultiplier}");
            Plugin.Log.LogInfo($"[LPS_Config] SafeZone : Enabled={SafeZoneEnabled} Radius={SafeZoneRadius}m");
            Plugin.Log.LogInfo($"[LPS_Config] SizeVariation : Enabled={SizeVariationEnabled} " +
                               $"Min={SizeVariationMin} Max={SizeVariationMax}");
            Plugin.Log.LogInfo($"[LPS_Config] RandomEvent : Enabled={RandomEventEnabled} " +
                               $"Interval=[{EventIntervalMin}-{EventIntervalMax}] min " +
                               $"PDAVoice={PDAVoiceEnabled}");
            Plugin.Log.LogInfo($"[LPS_Config] ApexPredatorHunt : Enabled={ApexPredatorHuntEnabled} " +
                               $"Weight={ApexPredatorHuntWeight}");
            Plugin.Log.LogInfo($"[LPS_Config] Migration : " +
                               $"Small={MigrationSmallEnabled}(w={MigrationSmallWeight}) " +
                               $"Medium={MigrationMediumEnabled}(w={MigrationMediumWeight}) " +
                               $"Large={MigrationLargeEnabled}(w={MigrationLargeWeight})");
            Plugin.Log.LogInfo($"[LPS_Config] DebugScanner : Enabled={DebugScannerEnabled}");
            Plugin.Log.LogInfo($"[LPS_Config] Blacklist : {cachedKeywords.Length} keywords.");
        }

        // Public properties — RSM
        public static float SpawnMultiplier => spawnMultiplier.Value;
        public static bool SafeZoneEnabled => safeZoneEnabled.Value;
        public static float SafeZoneRadius => safeZoneRadius.Value;

        // Public properties — SVM
        public static bool SizeVariationEnabled => sizeVariationEnabled.Value;
        public static float SizeVariationMin => sizeVariationMin.Value;
        public static float SizeVariationMax => sizeVariationMax.Value;

        // Public properties — REM
        public static bool RandomEventEnabled => randomEventEnabled.Value;
        public static bool DespawnAfterEvent => despawnAfterEvent.Value;
        public static bool PDAVoiceEnabled => pdaVoiceEnabled.Value;
        public static float EventIntervalMin => eventIntervalMin.Value;
        public static float EventIntervalMax => eventIntervalMax.Value;
        public static bool ApexPredatorHuntEnabled => apexPredatorHuntEnabled.Value;
        public static float ApexPredatorHuntWeight => apexPredatorHuntWeight.Value;

        // Public properties — Migration
        public static bool MigrationSmallEnabled => migrationSmallEnabled.Value;
        public static float MigrationSmallWeight => migrationSmallWeight.Value;
        public static bool MigrationMediumEnabled => migrationMediumEnabled.Value;
        public static float MigrationMediumWeight => migrationMediumWeight.Value;
        public static bool MigrationLargeEnabled => migrationLargeEnabled.Value;
        public static float MigrationLargeWeight => migrationLargeWeight.Value;

        // Public properties — Blacklist
        public static string[] ExcludedKeywords => cachedKeywords;

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
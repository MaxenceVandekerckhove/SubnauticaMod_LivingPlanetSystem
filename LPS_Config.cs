using BepInEx.Configuration;
using Nautilus.Options;
using Nautilus.Handlers;

namespace LivingPlanetSystem
{
    /// <summary>
    /// Global configuration for the LivingPlanetSystem mod.
    /// Settings are registered in Subnautica's in-game Mods menu via Nautilus.
    /// </summary>
    public static class LPS_Config
    {
        // Sections

        private const string SectionSpawn = "Spawn";

        // Config entries

        private static ConfigEntry<float> spawnMultiplier;

        // Nested ModOptions class

        /// Nautilus ModOptions implementation for the Living Planet System.
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

                // Subscribe to the change event after creation
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
        public static void Initialize(ConfigFile config)
        {
            // Bind to BepInEx config file
            spawnMultiplier = config.Bind(
                section: SectionSpawn,
                key: "SpawnMultiplier",
                defaultValue: 1.0f,
                description: "Global spawn rate multiplier applied to all creatures. " +
                             "1.0 = default rates | 0.1 = very rare | 10.0 = very frequent. " +
                             "Acceptable range: 0.1 to 10.0"
            );

            // Register in Subnautica's Mods menu via Nautilus
            OptionsPanelHandler.RegisterModOptions(new LPS_ModOptions());

            Plugin.Log.LogInfo($"[LPS_Config] Configuration loaded : SpawnMultiplier={SpawnMultiplier}");
        }

        /// Returns the current spawn multiplier value set by the player.
        public static float SpawnMultiplier => spawnMultiplier.Value;
    }
}
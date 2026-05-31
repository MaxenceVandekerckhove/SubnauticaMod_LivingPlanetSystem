using System.Collections.Generic;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Manual registry for modded creatures whose TechType is registered too late
    /// to be detected by RSM_CreatureRegistry during the main menu scan.
    /// 
    /// Add entries here with the exact TechType name and a magnitude measured
    /// via LPS_CreatureScanner in-game.
    /// 
    /// Entries are injected into RSM_CreatureCache.LoadCache() at runtime,
    /// making them fully visible to RSM_SpawnManager, REM_PredatorPool,
    /// REM_SwarmPool, and RSM_SafeZoneGuard.
    /// </summary>
    public static class RSM_ManualCreatureRegistry
    {
        // Manual creature entries
        // Format : (TechType name, magnitude measured via LPS_CreatureScanner)

        private static readonly List<(string techTypeName, float magnitude)> ManualEntries =
            new List<(string, float)>
            {
                ("IceDragon", 46.61f),
                ("PodshellLeviathan", 32.56f),
                ("PodshellLeviathanJuvenile", 14.47f),
                ("PodshellLeviathanBaby", 2.72f),
            };

        // Public API

        // Resolves all manual entries to TechType at runtime and returns valid ones.
        // Logs a warning for any entry whose TechType name cannot be resolved.
        public static List<(TechType techType, float magnitude)> GetEntries()
        {
            var result = new List<(TechType, float)>();

            foreach (var (techTypeName, magnitude) in ManualEntries)
            {
                bool resolved = TechTypeExtensions.FromString(techTypeName, out TechType techType, false);

                if (resolved)
                {
                    // Blacklist check
                    bool blacklisted = false;
                    foreach (string keyword in LPS_Config.ExcludedKeywords)
                    {
                        if (techTypeName.ToLower().Contains(keyword))
                        {
                            Plugin.Log.LogWarning($"[RSM_ManualCreatureRegistry] {techTypeName} excluded by blacklist : skipping.");
                            blacklisted = true;
                            break;
                        }
                    }

                    if (!blacklisted)
                    {
                        result.Add((techType, magnitude));
                        Plugin.Log.LogInfo($"[RSM_ManualCreatureRegistry] Resolved : {techTypeName} " +
                                           $"(magnitude={magnitude:F2}).");
                    }
                }
                else
                {
                    Plugin.Log.LogWarning($"[RSM_ManualCreatureRegistry] Could not resolve TechType : " +
                                          $"'{techTypeName}' — skipping.");
                }
            }

            Plugin.Log.LogInfo($"[RSM_ManualCreatureRegistry] {result.Count}/{ManualEntries.Count} " +
                               $"manual entries resolved.");

            return result;
        }

        // Builds a fingerprint string from the manual entries list.
        // Included in RSM_CreatureCache.BuildFingerprint() to invalidate the cache on changes.
        public static string Fingerprint()
        {
            var parts = new List<string>();

            foreach (var (techTypeName, magnitude) in ManualEntries)
                parts.Add($"{techTypeName}:{magnitude:F2}");

            parts.Sort();
            return string.Join("|", parts);
        }
    }
}
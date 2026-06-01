using System.Collections.Generic;
using LivingPlanetSystem.Core;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Responsible for evaluating parsed blacklist rules against creatures and biomes.
    ///
    /// Two evaluation entry points are exposed :
    ///
    ///   IsTotallyExcluded(TechType)
    ///     Returns true if the creature matches any TotalExclusion rule.
    ///     Used by RSM_CreatureFilter to remove the creature from the cache entirely.
    ///
    ///   FilterBiomes(TechType, List<BiomeType>)
    ///     Finds the first matching biome rule (BiomeExclude or BiomeOnly) for the
    ///     creature and returns the filtered biome list. If no biome rule matches,
    ///     the original list is returned unchanged.
    ///     Used by RSM_SpawnManager after the hard large-creature restriction has
    ///     already been applied.
    ///
    /// Matching rules :
    ///   - Creature keyword matching is partial and case-insensitive.
    ///   - Biome keyword matching is partial and case-insensitive.
    ///   - TotalExclusion rules are evaluated before biome rules regardless of
    ///     line order (a creature that is totally excluded never reaches biome filtering).
    ///   - For biome rules, the FIRST matching rule in the list wins.
    ///   - If a BiomeOnly rule leaves zero eligible biomes after filtering, the
    ///     result is an empty list and a warning is logged.
    /// </summary>
    public static class RSM_BlacklistEvaluator
    {
        // Public API

        // Returns true if the creature matches any TotalExclusion rule in the list.
        public static bool IsTotallyExcluded(TechType techType, List<RSM_CreatureRule> rules)
        {
            string name = techType.ToString().ToLower();

            foreach (RSM_CreatureRule rule in rules)
            {
                if (rule.Type != RSM_CreatureRule.RuleType.TotalExclusion)
                    continue;

                if (name.Contains(rule.CreatureKeyword))
                    return true;
            }

            return false;
        }

        // Finds the first matching biome rule for the creature and returns the
        // filtered biome list. Returns the original list unchanged if no rule matches.
        public static List<BiomeType> FilterBiomes(TechType techType,
                                                    List<BiomeType> eligibleBiomes,
                                                    List<RSM_CreatureRule> rules)
        {
            string name = techType.ToString().ToLower();

            foreach (RSM_CreatureRule rule in rules)
            {
                if (rule.Type == RSM_CreatureRule.RuleType.TotalExclusion)
                    continue;

                if (!name.Contains(rule.CreatureKeyword))
                    continue;

                // First matching biome rule wins
                if (rule.Type == RSM_CreatureRule.RuleType.BiomeExclude)
                    return ApplyExclude(techType, eligibleBiomes, rule);

                if (rule.Type == RSM_CreatureRule.RuleType.BiomeOnly)
                    return ApplyOnly(techType, eligibleBiomes, rule);
            }

            // No biome rule matched : return list unchanged
            return eligibleBiomes;
        }

        // Private helpers

        // Removes biomes whose name matches any keyword in the EXCLUDE rule.
        private static List<BiomeType> ApplyExclude(TechType techType,
                                                      List<BiomeType> biomes,
                                                      RSM_CreatureRule rule)
        {
            var result = new List<BiomeType>();

            foreach (BiomeType biome in biomes)
            {
                string biomeName = biome.ToString().ToLower();
                bool excluded = false;

                foreach (string keyword in rule.BiomeKeywords)
                {
                    if (biomeName.Contains(keyword.ToLower()))
                    {
                        excluded = true;
                        break;
                    }
                }

                if (!excluded)
                    result.Add(biome);
            }

            Plugin.Log.LogDebug($"[RSM_BlacklistEvaluator] {techType} EXCLUDE : " +
                                $"{biomes.Count} : {result.Count} biomes " +
                                $"(removed {biomes.Count - result.Count}).");

            return result;
        }

        // Keeps only biomes whose name matches at least one keyword in the ONLY rule.
        // Logs a warning if the result is empty.
        private static List<BiomeType> ApplyOnly(TechType techType,
                                                   List<BiomeType> biomes,
                                                   RSM_CreatureRule rule)
        {
            var result = new List<BiomeType>();

            foreach (BiomeType biome in biomes)
            {
                string biomeName = biome.ToString().ToLower();

                foreach (string keyword in rule.BiomeKeywords)
                {
                    if (biomeName.Contains(keyword.ToLower()))
                    {
                        result.Add(biome);
                        break;
                    }
                }
            }

            if (result.Count == 0)
            {
                Plugin.Log.LogWarning($"[RSM_BlacklistEvaluator] {techType} ONLY rule " +
                                      $"({string.Join(", ", rule.BiomeKeywords)}) " +
                                      $"left zero eligible biomes after hard rules : creature will be skipped.");
            }
            else
            {
                Plugin.Log.LogDebug($"[RSM_BlacklistEvaluator] {techType} ONLY : " +
                                    $"{biomes.Count} : {result.Count} biomes.");
            }

            return result;
        }
    }
}
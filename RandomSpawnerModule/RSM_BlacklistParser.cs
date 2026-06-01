using System.Collections.Generic;
using System.IO;
using LivingPlanetSystem.Core;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Responsible for parsing blacklist.txt into a list of LPS_CreatureRule objects.
    ///
    /// Parsing rules :
    ///   - Lines starting with # are comments and are ignored.
    ///   - Empty or whitespace-only lines are ignored.
    ///   - A line with no colon is a TotalExclusion rule.
    ///   - A line with " : EXCLUDE " produces a BiomeExclude rule.
    ///   - A line with " : ONLY " produces a BiomeOnly rule.
    ///   - Biome keywords are comma-separated and trimmed individually.
    ///   - A rule with an EXCLUDE or ONLY keyword but no biome keywords is
    ///     logged as a warning and skipped.
    ///   - Any line that does not match the expected syntax is logged as a
    ///     warning and skipped.
    /// </summary>
    public static class RSM_BlacklistParser
    {
        // Syntax tokens

        private const string TokenExclude = "EXCLUDE";
        private const string TokenOnly = "ONLY";
        private const char TokenSplit = ':';
        private const char TokenBiomeSep = ',';

        // Public API

        // Parses the file at the given path and returns the resulting rule list.
        // Returns an empty list if the file does not exist or cannot be read.
        public static List<RSM_CreatureRule> Parse(string filePath)
        {
            var rules = new List<RSM_CreatureRule>();

            if (!File.Exists(filePath))
            {
                Plugin.Log.LogWarning($"[RSM_BlacklistParser] File not found : {filePath}");
                return rules;
            }

            string[] lines = File.ReadAllLines(filePath);
            int lineNumber = 0;
            int parsed = 0;
            int skipped = 0;

            foreach (string raw in lines)
            {
                lineNumber++;
                string line = raw.Trim();

                // Skip comments and empty lines
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                RSM_CreatureRule rule = ParseLine(line, lineNumber);

                if (rule == null)
                {
                    skipped++;
                    continue;
                }

                rules.Add(rule);
                parsed++;
            }

            Plugin.Log.LogInfo($"[RSM_BlacklistParser] Parsing complete : " +
                               $"{parsed} rule(s) loaded, {skipped} line(s) skipped.");

            return rules;
        }

        // Private helpers

        // Parses a single non-comment line into a RSM_CreatureRule.
        // Returns null if the line is malformed.
        private static RSM_CreatureRule ParseLine(string line, int lineNumber)
        {
            int colonIndex = line.IndexOf(TokenSplit);

            // No colon → TotalExclusion
            if (colonIndex < 0)
            {
                string keyword = line.Trim();

                if (string.IsNullOrEmpty(keyword))
                {
                    Plugin.Log.LogWarning($"[RSM_BlacklistParser] Line {lineNumber} : empty keyword, skipping.");
                    return null;
                }

                return RSM_CreatureRule.Total(keyword);
            }

            // Split on first colon
            string creaturePart = line.Substring(0, colonIndex).Trim();
            string rulePart = line.Substring(colonIndex + 1).Trim();

            if (string.IsNullOrEmpty(creaturePart))
            {
                Plugin.Log.LogWarning($"[RSM_BlacklistParser] Line {lineNumber} : " +
                                      $"missing creature keyword before ':', skipping.");
                return null;
            }

            // Determine rule type from the first token of rulePart
            if (rulePart.StartsWith(TokenExclude))
            {
                string biomesRaw = rulePart.Substring(TokenExclude.Length).Trim();
                List<string> biomes = ParseBiomeKeywords(biomesRaw);

                if (biomes.Count == 0)
                {
                    Plugin.Log.LogWarning($"[RSM_BlacklistParser] Line {lineNumber} : " +
                                          $"EXCLUDE rule for '{creaturePart}' has no biome keywords, skipping.");
                    return null;
                }

                return RSM_CreatureRule.Exclude(creaturePart, biomes);
            }

            if (rulePart.StartsWith(TokenOnly))
            {
                string biomesRaw = rulePart.Substring(TokenOnly.Length).Trim();
                List<string> biomes = ParseBiomeKeywords(biomesRaw);

                if (biomes.Count == 0)
                {
                    Plugin.Log.LogWarning($"[RSM_BlacklistParser] Line {lineNumber} : " +
                                          $"ONLY rule for '{creaturePart}' has no biome keywords, skipping.");
                    return null;
                }

                return RSM_CreatureRule.Only(creaturePart, biomes);
            }

            Plugin.Log.LogWarning($"[RSM_BlacklistParser] Line {lineNumber} : " +
                                  $"unrecognized rule type in '{rulePart}' " +
                                  $"(expected EXCLUDE or ONLY), skipping.");
            return null;
        }

        // Splits a comma-separated biome string into a trimmed, non-empty keyword list.
        private static List<string> ParseBiomeKeywords(string raw)
        {
            var keywords = new List<string>();

            if (string.IsNullOrEmpty(raw))
                return keywords;

            foreach (string part in raw.Split(TokenBiomeSep))
            {
                string keyword = part.Trim();
                if (!string.IsNullOrEmpty(keyword))
                    keywords.Add(keyword);
            }

            return keywords;
        }
    }
}
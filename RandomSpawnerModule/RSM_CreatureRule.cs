using System.Collections.Generic;

namespace LivingPlanetSystem.Core
{
    /// <summary>
    /// Represents a single parsed rule from blacklist.txt.
    ///
    /// Three rule types are supported :
    ///   - TotalExclusion : the creature is excluded from all spawning.
    ///   - BiomeExclude   : the creature spawns everywhere EXCEPT the listed biome keywords.
    ///   - BiomeOnly      : the creature spawns ONLY in biomes matching the listed keywords.
    ///
    /// Both creature and biome matching use partial, case-insensitive keyword comparison.
    /// </summary>
    public class RSM_CreatureRule
    {
        // Rule type
        public enum RuleType
        {
            TotalExclusion,
            BiomeExclude,
            BiomeOnly
        }

        // Properties

        // The keyword used to match creature TechType names (partial, case-insensitive).
        public string CreatureKeyword { get; }

        // The type of rule to apply when the creature keyword matches.
        public RuleType Type { get; }

        // The list of biome keywords associated with this rule.
        // Empty for TotalExclusion rules.
        public IReadOnlyList<string> BiomeKeywords { get; }

        // Constructor

        public RSM_CreatureRule(string creatureKeyword, RuleType type, IReadOnlyList<string> biomeKeywords)
        {
            CreatureKeyword = creatureKeyword.ToLower().Trim();
            Type = type;
            BiomeKeywords = biomeKeywords ?? new List<string>();
        }

        // Factory helpers

        // Creates a TotalExclusion rule for the given creature keyword.
        public static RSM_CreatureRule Total(string creatureKeyword)
            => new RSM_CreatureRule(creatureKeyword, RuleType.TotalExclusion, null);

        // Creates a BiomeExclude rule for the given creature and biome keywords.
        public static RSM_CreatureRule Exclude(string creatureKeyword, IReadOnlyList<string> biomeKeywords)
            => new RSM_CreatureRule(creatureKeyword, RuleType.BiomeExclude, biomeKeywords);

        // Creates a BiomeOnly rule for the given creature and biome keywords.
        public static RSM_CreatureRule Only(string creatureKeyword, IReadOnlyList<string> biomeKeywords)
            => new RSM_CreatureRule(creatureKeyword, RuleType.BiomeOnly, biomeKeywords);
        public override string ToString()
        {
            switch (Type)
            {
                case RuleType.TotalExclusion:
                    return $"[TOTAL] {CreatureKeyword}";
                case RuleType.BiomeExclude:
                    return $"[EXCLUDE] {CreatureKeyword} : {string.Join(", ", BiomeKeywords)}";
                case RuleType.BiomeOnly:
                    return $"[ONLY] {CreatureKeyword} : {string.Join(", ", BiomeKeywords)}";
                default:
                    return CreatureKeyword;
            }
        }
    }
}
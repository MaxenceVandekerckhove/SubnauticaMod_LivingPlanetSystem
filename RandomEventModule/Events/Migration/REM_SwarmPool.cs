using System;
using System.Collections.Generic;
using LivingPlanetSystem.RandomSpawnerModule;

namespace LivingPlanetSystem.RandomEventModule.Events.Migration
{
    /// <summary>
    /// Responsible for building the creature pool for migration eventsand returning a SwarmComposition — the chosen TechType plus adult and juvenile counts — based on the migration category.
    ///
    /// Three migration categories are supported :
    ///   - Small  : small creatures only  (magnitude strictly below MagnitudeSmall)
    ///   - Medium : medium creatures only (magnitude between MagnitudeSmall and MagnitudeMedium)
    ///   - Large  : large creatures only  (magnitude above MagnitudeMedium, or forced large by name)
    ///
    /// </summary>
    public static class REM_SwarmPool
    {
        // Blacklist — creatures excluded from all migration events regardless of size
        private static readonly string[] Blacklist =
        {
        };

        // Adult count ranges per migration category

        private const int AdultCountSmallMin = 25;
        private const int AdultCountSmallMax = 40;
        private const int AdultCountMediumMin = 10;
        private const int AdultCountMediumMax = 20;
        private const int AdultCountLargeMin = 1;
        private const int AdultCountLargeMax = 3;

        // Juvenile count ranges per migration category

        private const int JuvenileCountSmallMin = 15;
        private const int JuvenileCountSmallMax = 25;
        private const int JuvenileCountMediumMin = 5;
        private const int JuvenileCountMediumMax = 10;
        private const int JuvenileCountLargeMin = 1;
        private const int JuvenileCountLargeMax = 3;

        // Public data structure

        /// Describes the full composition of a migration swarm : the chosen species, how many adults, and how many juveniles.
        public class SwarmComposition
        {
            public TechType TechType { get; set; }
            public int AdultCount { get; set; }
            public int JuvenileCount { get; set; }
            public int TotalCount => AdultCount + JuvenileCount;
        }

        // Public API

        /// Builds an eligible pool for the given migration category, picks one creature at random, and returns a SwarmComposition.
        public static SwarmComposition Build(REM_MigrationCategory category, Random random)
        {
            var cache = RSM_CreatureCache.LoadCache();
            var pool = new List<TechType>();

            int skippedWrongSize = 0;
            int skippedBlacklist = 0;

            foreach (var (techType, magnitude) in cache)
            {
                // Size filter
                if (!MatchesCategory(techType, magnitude, category))
                {
                    skippedWrongSize++;
                    continue;
                }

                // Blacklist filter
                if (IsBlacklisted(techType))
                {
                    Plugin.Log.LogDebug($"[REM_SwarmPool] {techType} blacklisted : skipping.");
                    skippedBlacklist++;
                    continue;
                }

                pool.Add(techType);
            }

            Plugin.Log.LogInfo($"[REM_SwarmPool] Pool built for {category} : " +
                               $"{pool.Count} eligible creature(s) " +
                               $"(skippedWrongSize={skippedWrongSize}, " +
                               $"skippedBlacklist={skippedBlacklist}).");

            if (pool.Count == 0)
            {
                Plugin.Log.LogWarning($"[REM_SwarmPool] No eligible creature found for {category} : aborting.");
                return null;
            }

            TechType chosen = pool[random.Next(pool.Count)];

            SwarmComposition composition = BuildComposition(chosen, category, random);

            Plugin.Log.LogInfo($"[REM_SwarmPool] Chosen : {chosen} " +
                               $": {composition.AdultCount} adult(s), " +
                               $"{composition.JuvenileCount} juvenile(s) " +
                               $"(total={composition.TotalCount}).");

            return composition;
        }

        // Private helpers

        /// Returns true if the creature matches the size category requested.
        private static bool MatchesCategory(TechType techType, float magnitude, REM_MigrationCategory category)
        {
            bool forcedLarge = RSM_SpawnManager.IsLargeByName(techType);

            switch (category)
            {
                case REM_MigrationCategory.Small:
                    // Must be strictly small : Not forced large, magnitude below Small threshold
                    return !forcedLarge
                        && !RSM_SpawnManager.IsLargeCategory(magnitude)
                        && magnitude < RSM_SpawnManager.MagnitudeSmall;

                case REM_MigrationCategory.Medium:
                    // Must be medium : Not forced large, magnitude between Small and Medium thresholds
                    return !forcedLarge
                        && !RSM_SpawnManager.IsLargeCategory(magnitude)
                        && magnitude >= RSM_SpawnManager.MagnitudeSmall;

                case REM_MigrationCategory.Large:
                    // Must be large : Either forced by name or above the Large magnitude threshold
                    return forcedLarge || RSM_SpawnManager.IsLargeCategory(magnitude);

                default:
                    return false;
            }
        }

        /// Randomizes adult and juvenile counts for the chosen creature and category.
        private static SwarmComposition BuildComposition(TechType techType, REM_MigrationCategory category, Random random)
        {
            int adultMin, adultMax, juvenileMin, juvenileMax;

            switch (category)
            {
                case REM_MigrationCategory.Small:
                    adultMin = AdultCountSmallMin;
                    adultMax = AdultCountSmallMax;
                    juvenileMin = JuvenileCountSmallMin;
                    juvenileMax = JuvenileCountSmallMax;
                    break;

                case REM_MigrationCategory.Medium:
                    adultMin = AdultCountMediumMin;
                    adultMax = AdultCountMediumMax;
                    juvenileMin = JuvenileCountMediumMin;
                    juvenileMax = JuvenileCountMediumMax;
                    break;

                case REM_MigrationCategory.Large:
                    adultMin = AdultCountLargeMin;
                    adultMax = AdultCountLargeMax;
                    juvenileMin = JuvenileCountLargeMin;
                    juvenileMax = JuvenileCountLargeMax;
                    break;

                default:
                    adultMin = 1;
                    adultMax = 1;
                    juvenileMin = 0;
                    juvenileMax = 0;
                    break;
            }

            return new SwarmComposition
            {
                TechType = techType,
                AdultCount = random.Next(adultMin, adultMax + 1),
                JuvenileCount = random.Next(juvenileMin, juvenileMax + 1),
            };
        }

        /// Returns true if the creature name contains any SwarmPool blacklist keyword.
        private static bool IsBlacklisted(TechType techType)
        {
            string name = techType.ToString().ToLower();

            foreach (string keyword in Blacklist)
            {
                if (name.Contains(keyword))
                    return true;
            }

            return false;
        }
    }
}
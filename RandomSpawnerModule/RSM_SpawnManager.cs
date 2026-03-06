using LivingPlanetSystem.Core;
using Nautilus.Handlers;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Responsible for generating randomized creature spawn distributions
    /// and registering them with Subnautica's LootDistributionHandler.
    /// 
    /// For each creature in the cache :
    ///   - Retrieves the list of eligible biomes based on creature size
    ///   - Picks a random subset of biomes for this creature
    ///   - Assigns a random probability per biome influenced by creature size
    ///   - Assigns a count per biome inversely proportional to creature size
    ///   - Applies the global spawn multiplier from LPS_Config
    ///   - Leviathan-named creatures are restricted to unrestricted biomes only,
    ///     regardless of their measured magnitude.
    /// 
    /// Size is determined by the average of the 3 bounding box axes.
    /// All randomness is driven by LPS_SeedManager for session consistency.
    /// </summary>
    public static class RSM_SpawnManager
    {
        // ── Size categories ───────────────────────────────────────────────────────

        public const float MagnitudeSmall = 1.5f;
        public const float MagnitudeMedium = 16f;

        // ── Biome assignment limits ───────────────────────────────────────────────

        private const int BiomeCountSmallMin = 40;
        private const int BiomeCountSmallMax = 80;
        private const int BiomeCountMediumMin = 20;
        private const int BiomeCountMediumMax = 60;
        private const int BiomeCountLargeMin = 10;
        private const int BiomeCountLargeMax = 50;

        // ── Probability ranges per size category ──────────────────────────────────

        // Small
        private const float ProbSmallMin = 0.00025f;
        private const float ProbSmallMax = 0.006f;

        // Medium
        private const float ProbMediumMin = 0.00015f;
        private const float ProbMediumMax = 0.004f;

        // Large
        private const float ProbLargeMin = 0.00009f;
        private const float ProbLargeMax = 0.001f;

        // ── Count ranges per size category ───────────────────────────────────────

        private const int CountSmallMin = 1;
        private const int CountSmallMax = 4;
        private const int CountMediumMin = 1;
        private const int CountMediumMax = 3;
        private const int CountLarge = 1;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads the creature cache and registers randomized spawn distributions
        /// for all creatures via LootDistributionHandler.
        /// Should be called once when the game world is loaded (Aurora scene).
        /// </summary>
        public static void RegisterSpawns()
        {
            Plugin.Log.LogInfo("[RSM_SpawnManager] Starting spawn registration...");

            var creatures = RSM_CreatureCache.LoadCache();

            if (creatures.Count == 0)
            {
                Plugin.Log.LogWarning("[RSM_SpawnManager] No creatures in cache — skipping spawn registration.");
                return;
            }

            float multiplier = LPS_Config.SpawnMultiplier;
            var random = LPS_SeedManager.Random;
            int registered = 0;

            // Log creature classification summary
            int countSmall = 0, countMedium = 0, countLarge = 0;

            var largeList = new List<string>();
            var mediumList = new List<string>();
            var smallList = new List<string>();

            foreach (var (techType, magnitude) in creatures)
            {
                bool forcedLarge = IsLargeByName(techType);
                bool isLarge = IsLargeCategory(magnitude) || forcedLarge;

                if (isLarge)
                {
                    countLarge++;
                    largeList.Add($"{techType} ({magnitude:F2}{(forcedLarge ? "*" : "")}) [RESTRICTED]");
                }
                else if (IsMediumCategory(magnitude))
                {
                    countMedium++;
                    mediumList.Add($"{techType} ({magnitude:F2}) [OPEN]");
                }
                else
                {
                    countSmall++;
                    smallList.Add($"{techType} ({magnitude:F2}) [OPEN]");
                }
            }

            Plugin.Log.LogInfo($"[RSM_SpawnManager] Size classification : " +
                               $"Small: {countSmall}  Medium: {countMedium}  Large: {countLarge}  (* = forced by name)");

            Plugin.Log.LogInfo("[RSM_SpawnManager] LARGE creatures (RESTRICTED biomes only) :");
            foreach (string entry in largeList)
                Plugin.Log.LogInfo($"[RSM_SpawnManager]   {entry}");

            Plugin.Log.LogInfo("[RSM_SpawnManager] MEDIUM creatures (all biomes) :");
            foreach (string entry in mediumList)
                Plugin.Log.LogInfo($"[RSM_SpawnManager]   {entry}");

            Plugin.Log.LogInfo("[RSM_SpawnManager] SMALL creatures (all biomes) :");
            foreach (string entry in smallList)
                Plugin.Log.LogInfo($"[RSM_SpawnManager]   {entry}");

            foreach (var (techType, magnitude) in creatures)
            {
                try
                {
                    bool forceLarge = IsLargeByName(techType);
                    List<BiomeType> eligibleBiomes = RSM_BiomeClassifier.GetEligibleBiomes(magnitude, forceLarge);

                    if (eligibleBiomes.Count == 0)
                    {
                        Plugin.Log.LogWarning($"[RSM_SpawnManager] No eligible biomes for {techType} — skipping.");
                        continue;
                    }

                    int biomeCount = GetBiomeCount(magnitude, forceLarge, eligibleBiomes.Count, multiplier, random);
                    List<BiomeType> selectedBiomes = PickRandomBiomes(eligibleBiomes, biomeCount, random);

                    var biomeDataList = new List<LootDistributionData.BiomeData>();

                    foreach (BiomeType biome in selectedBiomes)
                    {
                        float probability = GenerateProbability(magnitude, forceLarge, multiplier, random);
                        int count = GenerateCount(magnitude, forceLarge, random);

                        biomeDataList.Add(new LootDistributionData.BiomeData
                        {
                            biome = biome,
                            probability = probability,
                            count = count
                        });
                    }

                    string classId = CraftData.GetClassIdForTechType(techType);

                    if (string.IsNullOrEmpty(classId))
                    {
                        Plugin.Log.LogWarning($"[RSM_SpawnManager] Could not get classId for {techType} — skipping.");
                        continue;
                    }

                    LootDistributionHandler.EditLootDistributionData(classId, biomeDataList);

                    Plugin.Log.LogDebug($"[RSM_SpawnManager] Registered {techType} " +
                                        $"in {selectedBiomes.Count}/{eligibleBiomes.Count} biomes " +
                                        $"(magnitude={magnitude:F2}, forcedLarge={forceLarge}).");
                    registered++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[RSM_SpawnManager] Failed to register {techType} : {e.Message}");
                }
            }

            Plugin.Log.LogInfo($"[RSM_SpawnManager] Spawn registration complete : " +
                               $"{registered}/{creatures.Count} creatures registered.");
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the creature name contains "leviathan".
        /// Used exclusively to restrict biome eligibility — does not affect
        /// size categorization, spawn probability, or count.
        /// </summary>
        private static bool IsLargeByName(TechType techType)
        {
            return techType.ToString().ToLower().Contains("leviathan");
        }

        /// <summary>
        /// Returns true if the creature falls in the Large size category.
        /// </summary>
        private static bool IsLargeCategory(float magnitude)
        {
            return magnitude >= MagnitudeMedium;
        }

        /// <summary>
        /// Returns true if the creature falls in the Medium size category.
        /// </summary>
        private static bool IsMediumCategory(float magnitude)
        {
            return magnitude >= MagnitudeSmall && magnitude < MagnitudeMedium;
        }

        /// <summary>
        /// Returns the number of biomes to assign to a creature based on its size category.
        /// Clamped to the number of available eligible biomes.
        /// </summary>
        private static int GetBiomeCount(float magnitude, bool forceLarge, int maxAvailable, float multiplier, Random random)
        {
            int min, max;

            if (IsLargeCategory(magnitude) || forceLarge)
            {
                min = BiomeCountLargeMin;
                max = BiomeCountLargeMax;
            }
            else if (IsMediumCategory(magnitude))
            {
                min = BiomeCountMediumMin;
                max = BiomeCountMediumMax;
            }
            else
            {
                min = BiomeCountSmallMin;
                max = BiomeCountSmallMax;
            }

            int baseCount = random.Next(min, max + 1);
            float scale = 1f + (float)(Math.Log10(multiplier) * 0.5);
            int scaled = (int)Math.Round(baseCount * scale);
            return Math.Max(1, Math.Min(scaled, maxAvailable));
        }

        /// <summary>
        /// Picks a random subset of biomes using a Fisher-Yates shuffle.
        /// </summary>
        private static List<BiomeType> PickRandomBiomes(List<BiomeType> eligible, int count, Random random)
        {
            List<BiomeType> shuffled = new List<BiomeType>(eligible);

            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                BiomeType temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            return shuffled.GetRange(0, count);
        }

        /// <summary>
        /// Generates a random spawn probability based on creature size category.
        /// The global spawn multiplier is applied and the result is clamped to [0.0001, 1.0].
        /// </summary>
        private static float GenerateProbability(float magnitude, bool forceLarge, float multiplier, Random random)
        {
            float min, max;

            if (IsLargeCategory(magnitude) || forceLarge)
            {
                min = ProbLargeMin;
                max = ProbLargeMax;
            }
            else if (IsMediumCategory(magnitude))
            {
                min = ProbMediumMin;
                max = ProbMediumMax;
            }
            else
            {
                min = ProbSmallMin;
                max = ProbSmallMax;
            }

            float baseProbability = min + (float)random.NextDouble() * (max - min);
            return Math.Max(0.0001f, Math.Min(1.0f, baseProbability * multiplier));
        }

        /// <summary>
        /// Generates a spawn count based on creature size category.
        /// Large creatures always spawn alone.
        /// </summary>
        private static int GenerateCount(float magnitude, bool forceLarge, Random random)
        {
            if (IsLargeCategory(magnitude) || forceLarge)
                return CountLarge;

            if (IsMediumCategory(magnitude))
                return random.Next(CountMediumMin, CountMediumMax + 1);

            return random.Next(CountSmallMin, CountSmallMax + 1);
        }
    }
}
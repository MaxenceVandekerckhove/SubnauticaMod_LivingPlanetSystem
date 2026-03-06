using System;

namespace LivingPlanetSystem.Core
{
    /// <summary>
    /// Responsible for generating and providing the global world seed.
    /// The seed is regenerated every game launch, ensuring a different ecosystem configuration each session.
    /// All LPS modules that require randomness should use this seed to ensure consistent and reproducible results within a session.
    /// </summary>
    public static class LPS_SeedManager
    {
    
        // Private state

        private static int currentSeed;
        private static Random seededRandom;
        private static bool isInitialized = false;

        // Public API

        /// Generates a new random seed for this session.
        public static void Initialize()
        {
            if (isInitialized)
            {
                Plugin.Log.LogWarning("[LPS_SeedManager] Already initialized : skipping.");
                return;
            }

            // Generate a random seed based on current time
            currentSeed = new Random().Next(int.MinValue, int.MaxValue);
            seededRandom = new Random(currentSeed);
            isInitialized = true;

            Plugin.Log.LogInfo($"[LPS_SeedManager] New session seed generated : {currentSeed}");
        }

        /// Returns the current session seed.
        public static int CurrentSeed => currentSeed;

        /// Returns the shared Random instance seeded with the session seed.
        public static Random Random => seededRandom;

        /// Resets the seed manager.
        public static void Clear()
        {
            Plugin.Log.LogInfo("[LPS_SeedManager] Clearing seed manager.");
            isInitialized = false;
            seededRandom = null;
        }
    }
}
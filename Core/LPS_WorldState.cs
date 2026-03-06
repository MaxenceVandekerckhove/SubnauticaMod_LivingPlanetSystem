namespace LivingPlanetSystem.Core
{
    /// <summary>
    /// Central repository for the living world's ecological state.
    /// Currently a skeleton — will be populated by future LPS modules :
    ///   - LPS Ecosystem Dynamics  : population counts per biome
    ///   - LPS Migration System    : active migration routes
    ///   - LPS Environmental Pressure : zone stress levels
    ///   - LPS Player Impact       : player activity memory
    ///   - LPS Events and Anomalies : active world events
    /// </summary>
    public static class LPS_WorldState
    {
        // Public API

        /// Initializes the world state for a new session.
        public static void Initialize()
        {
            Plugin.Log.LogInfo("[LPS_WorldState] World state initialized : ready for module population.");
        }

        /// Clears all world state data.
        public static void Clear()
        {
            Plugin.Log.LogInfo("[LPS_WorldState] Clearing world state.");
        }
    }
}
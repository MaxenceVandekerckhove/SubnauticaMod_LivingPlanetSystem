using System.Collections.Generic;
using UnityEngine;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Stores the center of the spawn safe zone in memory.
    /// Persistence is handled entirely by LPS_SeedManager, which reads and writes
    /// the center coordinates as part of the per-slot JSON file.
    ///
    /// LPS_SeedManager calls Initialize() after loading or capturing the center.
    /// Plugin calls Clear() on main menu load to reset in-memory state.
    ///
    /// Distance is evaluated on the XZ plane only : a creature spawning directly
    /// below the safe zone center is just as unwanted as one at the surface.
    ///
    /// The module is disabled when LPS_Config.SafeZoneEnabled is false.
    /// </summary>
    public static class RSM_SafeZone
    {
        // Private state

        private static Vector3 center = Vector3.zero;
        private static bool isInitialized = false;

        private static Dictionary<TechType, float> cachedMagnitudes = new Dictionary<TechType, float>();

        // Public API

        // Sets the safe zone center. Called by LPS_SeedManager after loading or capturing the center for the current slot. Y is forced to 0.
        public static void Initialize(Vector3 center)
        {
            RSM_SafeZone.center = new Vector3(center.x, 0f, center.z);
            isInitialized = true;

            cachedMagnitudes.Clear();
            foreach (var (techType, magnitude) in RSM_CreatureCache.LoadCache())
                cachedMagnitudes[techType] = magnitude;

            Plugin.Log.LogInfo($"[RSM_SafeZone] Center set : ({RSM_SafeZone.center.x:F0}, {RSM_SafeZone.center.z:F0}), " +
                               $"radius={LPS_Config.SafeZoneRadius}m.");
        }

        // Resets in-memory state. Called on main menu load.
        public static void Clear()
        {
            center = Vector3.zero;
            isInitialized = false;
            cachedMagnitudes.Clear();

            Plugin.Log.LogInfo("[RSM_SafeZone] In-memory state cleared.");
        }

        // Returns true if the given world position falls within the safe zone sphere.
        // Always returns false when the module is disabled or not initialized.
        public static bool IsInsideSafeZone(Vector3 worldPosition)
        {
            if (!LPS_Config.SafeZoneEnabled || !isInitialized)
                return false;

            float radius = LPS_Config.SafeZoneRadius;
            return Vector3.SqrMagnitude(worldPosition - center) <= radius * radius;
        }

        /// Returns the cached magnitude for a given TechType.
        public static bool TryGetMagnitude(TechType techType, out float magnitude)
        {
            return cachedMagnitudes.TryGetValue(techType, out magnitude);
        }
    }
}
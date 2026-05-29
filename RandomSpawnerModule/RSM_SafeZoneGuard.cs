using HarmonyLib;
using System;
using UnityEngine;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Harmony postfix on Creature.Start.
    /// When a creature spawns inside the safe zone, this guard checks whether it
    /// should be immediately destroyed based on three cumulative conditions :
    ///
    ///   1. The creature is Medium or Large (magnitude >= RSM_SpawnManager.MagnitudeSmall),
    ///      or is forced into the Large category by name (leviathan, seatreader, etc.).
    ///   2. The creature has at least one aggressive component :
    ///      AggressiveWhenSeeTarget or AggressiveToPilotingVehicle.
    ///   3. The creature's spawn position falls within the safe zone radius
    ///      (XZ distance from the player's initial spawn point).
    ///
    /// Magnitude is looked up from the RSM creature cache at runtime.
    /// If a creature's TechType is not in the cache (mod-added creatures loaded
    /// before the cache was built, or creatures that failed the scan), it is
    /// treated as Small and left alone.
    ///
    /// The guard is entirely skipped when LPS_Config.SafeZoneEnabled is false,
    /// adding zero overhead to the normal spawn path.
    /// </summary>
  
    [HarmonyPatch(typeof(Creature), nameof(Creature.Start))]
    public static class RSM_SafeZoneGuard
    {
        [HarmonyPostfix]
        public static void Postfix(Creature __instance)
        {
            if (!LPS_Config.SafeZoneEnabled)
                return;

            try
            {
                // 1. Position check first
                Vector3 position = __instance.transform.position;
                if (!RSM_SafeZone.IsInsideSafeZone(position))
                    return;

                // 2. Resolve TechType
                TechType techType = CraftData.GetTechType(__instance.gameObject);
                if (techType == TechType.None)
                    return;

                // 3. Size check via cache
                if (!IsMediumOrLarge(techType))
                    return;

                // 4. Aggression check
                if (!IsAggressive(__instance))
                    return;

                // All conditions met : destroy the creature
                Plugin.Log.LogInfo($"[RSM_SafeZoneGuard] Destroying {techType} at {position} " +
                                   $"(inside safe zone, aggressive, Medium/Large).");

                UnityEngine.Object.Destroy(__instance.gameObject);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[RSM_SafeZoneGuard] Error on {__instance.name} : {e.Message}");
            }
        }

        // Private helpers

        // Returns true if the creature is Medium or Large according to the RSM cache, or if it is forced Large by name keyword.
        // Unknown creatures (not in cache) default to Small → return false.
        private static bool IsMediumOrLarge(TechType techType)
        {
            if (RSM_SpawnManager.IsLargeByName(techType))
                return true;

            if (RSM_SafeZone.TryGetMagnitude(techType, out float magnitude))
                return magnitude >= RSM_SpawnManager.MagnitudeSmall;

            return false;
        }

        // Returns true if the creature has at least one aggressive component.
        private static bool IsAggressive(Creature creature)
        {
            return
                creature.GetComponentInChildren<AggressiveWhenSeeTarget>(includeInactive: true) != null ||
                creature.GetComponentInChildren<AggressiveToPilotingVehicle>(includeInactive: true) != null;
        }
    }
}
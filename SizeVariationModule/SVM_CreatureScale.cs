using HarmonyLib;
using UnityEngine;
using System;

namespace LivingPlanetSystem.SizeVariationModule
{
    /// <summary>
    /// Applies a randomized uniform scale to each creature at spawn time.
    /// Scale is determined once per creature instance using the session seed,
    /// within the range defined by SizeVariationMin and SizeVariationMax in LPS_Config.
    /// 
    /// The module can be enabled or disabled via LPS_Config.SizeVariationEnabled.
    /// </summary>
    [HarmonyPatch(typeof(Creature), nameof(Creature.Start))]
    public static class SVM_CreatureScale
    {
        [HarmonyPostfix]
        public static void Postfix(Creature __instance)
        {
            if (!LPS_Config.SizeVariationEnabled)
                return;

            try
            {
                float min = LPS_Config.SizeVariationMin;
                float max = LPS_Config.SizeVariationMax;

                if (min > max)
                {
                    Plugin.Log.LogWarning($"[SVM_CreatureScale] SizeVariationMin ({min}) > SizeVariationMax ({max}) " +
                                          $"— applying default range (0.5 - 1.8).");
                    min = 0.5f;
                    max = 1.8f;
                }

                float scale = min + (float)LPS_SeedManager.Random.NextDouble() * (max - min);

                __instance.transform.localScale = Vector3.one * scale;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[SVM_CreatureScale] Failed to apply scale to {__instance.name} : {e.Message}");
            }
        }
    }
}
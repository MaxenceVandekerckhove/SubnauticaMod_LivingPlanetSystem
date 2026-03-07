using HarmonyLib;
using LivingPlanetSystem;

namespace LivingPlanetSystem.Patches
{
    [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.ClearSlotAsync))]
    public static class SaveLoadManager_ClearSlot_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(string slotName)
        {
            Plugin.Log.LogInfo($"[Plugin] Save slot '{slotName}' deleted : cleaning seed.");
            LPS_SeedManager.DeleteSeedForSlot(slotName);
        }
    }
}
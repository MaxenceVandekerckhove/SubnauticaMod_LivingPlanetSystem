using BepInEx;
using BepInEx.Logging;

namespace LivingPlanetSystem
{
    [BepInPlugin(MyGuid, PluginName, Version)]
    public class Plugin : BaseUnityPlugin
    {
        // Mod details
        private const string MyGuid = "com.CaporalCross.LivingPlanetSystem";
        private const string PluginName = "LivingPlanetSystem";
        private const string Version = "1.0.0";

        // Logger instance
        public static ManualLogSource Log = new ManualLogSource(PluginName);

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} v{Version} is loaded!");
            Log = Logger;
        }
    }
}

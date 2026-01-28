using BepInEx;
using BepInEx.Logging;
using LivingPlanetSystem.RandomSpawnerModule;
using UnityEngine;
using UnityEngine.SceneManagement;

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

            // Every time a scene is loaded, call OnSceneLoaded method
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Aurora => Player started a World
            if (scene.name == "Aurora")
            {
                // Create persistent runner object
                GameObject runner = new GameObject("RSM_CoreRunner");
                DontDestroyOnLoad(runner);

                runner.AddComponent<RSM_PlayerPositionTracker>();

                RSM_BiomeRegistry.RegisterAllBiomes();
            }
        }
    }
}

using BepInEx;
using BepInEx.Logging;
using LivingPlanetSystem.RandomSpawnerModule;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace LivingPlanetSystem
{
    [BepInPlugin(MyGuid, PluginName, Version)]
    public class Plugin : BaseUnityPlugin
    {
        // Constants
        private const string MyGuid = "com.CaporalCross.LivingPlanetSystem";
        private const string PluginName = "LivingPlanetSystem";
        private const string Version = "1.0.0";

        // Public static logger (accessible from all classes)
        public static ManualLogSource Log;

        // Lifecycle

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{PluginName} v{Version} loaded.");

            // Subscribe to scene changes to detect menu and game scenes
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            // Always unsubscribe to avoid ghost callbacks if the plugin is unloaded
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Scene handling

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogInfo($"[Plugin] Scene loaded : {scene.name}");

            switch (scene.name)
            {
                case "XMenu":
                    // Main menu is loaded
                    OnMainMenuLoaded();
                    break;

                case "Aurora":
                    // Game world is loaded
                    OnGameWorldLoaded();
                    break;
            }
        }

        // Scene callbacks
        private void OnMainMenuLoaded()
        {
            Plugin.Log.LogInfo("[Plugin] Main menu detected : initializing RSM systems.");

            // Step 1 : initialize biome registry (synchronous)
            RSM_BiomeRegistry.Initialize();

            // Step 2 : check cache validity
            if (RSM_CreatureCache.IsCacheValid())
            {
                // Cache is valid — no need to scan
                Plugin.Log.LogInfo("[Plugin] Creature cache is valid : skipping scan.");
                // TODO : trigger RSM_SpawnManager directly
            }
            else
            {
                // Cache is invalid or missing — start scan
                Plugin.Log.LogInfo("[Plugin] Creature cache is invalid : starting scan.");
                RSM_CreatureRegistry.OnScanCompleted += OnCreatureScanCompleted;
                RSM_CreatureRegistry.StartScan();
            }
        }

        private void OnGameWorldLoaded()
        {
            Log.LogInfo("[Plugin] Game world detected : spawn registration will start here.");
            // TODO : trigger RSM_SpawnManager
        }

        /// Called when RSM_CreatureRegistry finishes scanning all TechTypes.
        private void OnCreatureScanCompleted(List<TechType> rawCreatures)
        {
            Plugin.Log.LogInfo($"[Plugin] Creature scan completed : {rawCreatures.Count} raw creatures found.");

            // Unsubscribe immediately to avoid duplicate calls
            RSM_CreatureRegistry.OnScanCompleted -= OnCreatureScanCompleted;

            // TODO : pass to RSM_CreatureFilter
        }
    }
}

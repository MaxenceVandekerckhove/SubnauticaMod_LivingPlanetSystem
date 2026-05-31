using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LivingPlanetSystem.Core;
using LivingPlanetSystem.RandomEventModule;
using LivingPlanetSystem.RandomSpawnerModule;
using Nautilus.Handlers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UWE;

namespace LivingPlanetSystem
{
    [BepInPlugin(MyGuid, PluginName, Version)]
    public class Plugin : BaseUnityPlugin
    {
        // Constants
        private const string MyGuid = "com.CaporalCross.LivingPlanetSystem";
        private const string PluginName = "LivingPlanetSystem";
        private const string Version = "1.3.0";

        // Public static logger (accessible from all classes)
        public static ManualLogSource Log;

        // Lifecycle

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{PluginName} v{Version} loaded.");

            // Initialize Harmony and patch
            Harmony harmony = new Harmony(MyGuid);
            harmony.PatchAll();

            // Initialize global configuration
            LPS_Config.Initialize(Config);

            // Initialize core systems
            LPS_WorldState.Initialize();

            WaitScreenHandler.RegisterAsyncLoadTask(
                modName: PluginName,
                loadingFunction: AsyncLoadTask,
                description: "Scanning creatures..."
            );

            // Subscribe to scene changes to detect menu and game scenes
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private void OnDestroy()
        {
            // Always unsubscribe to avoid ghost callbacks if the plugin is unloaded
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Late load task to run after the main menu is loaded but before the player can play
        private static IEnumerator AsyncLoadTask(WaitScreenHandler.WaitScreenTask task)
        {
            Plugin.Log.LogInfo("[Plugin] AsyncLoadTask started.");

            task.Status = "Initializing biome registry...";
            RSM_BiomeRegistry.Initialize();
            RSM_BiomeClassifier.Initialize();

            task.Status = "Checking creature cache...";

            if (RSM_CreatureCache.IsCacheValid())
            {
                Plugin.Log.LogInfo("[Plugin] Creature cache is valid : skipping scan.");
            }
            else
            {
                Plugin.Log.LogInfo("[Plugin] Creature cache is invalid : starting scan...");

                task.Status = "Scanning creatures...";

                List<TechType> rawCreatures = null;
                bool scanDone = false;

                RSM_CreatureRegistry.OnScanCompleted += result =>
                {
                    rawCreatures = result;
                    scanDone = true;
                };

                RSM_CreatureRegistry.StartScan();

                while (!scanDone)
                    yield return null;

                task.Status = "Filtering creatures...";

                bool filterDone = false;
                yield return RSM_CreatureFilter.Filter(rawCreatures, () => filterDone = true);

                while (!filterDone)
                    yield return null;

                RSM_CreatureCache.SaveCache(RSM_CreatureFilter.GetFilteredCreatures());
                Plugin.Log.LogInfo("[Plugin] Cache saved.");
            }

            Plugin.Log.LogInfo("[Plugin] AsyncLoadTask complete.");
        }

        // Scene handling
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogInfo($"[Plugin] Scene loaded : {scene.name}");

            switch (scene.name)
            {
                case "XMenu":
                    OnMainMenuLoaded();
                    break;

                case "Aurora":
                    OnGameWorldLoaded();
                    break;
            }
        }

        // Scene callbacks
        private void OnMainMenuLoaded()
        {
            Plugin.Log.LogInfo("[Plugin] Main menu detected : resetting session state.");

            REM_EventTimer.Stop();
            RSM_SafeZone.Clear();
            RSM_BiomeRegistry.Clear();
            RSM_BiomeClassifier.Clear();
            RSM_CreatureRegistry.Clear();
        }

        private void OnGameWorldLoaded()
        {
            Plugin.Log.LogInfo("[Plugin] Game world detected : registering spawns.");

            LPS_SeedManager.InitializeForCurrentSlot();

            RSM_SpawnManager.RegisterSpawns();

            REM_EventRegistry.Initialize();
            REM_EventTimer.Start();

            if (LPS_Config.DebugScannerEnabled)
            {
                gameObject.AddComponent<LPS_CreatureScanner>();
                Plugin.Log.LogInfo("[Plugin] LPS_CreatureScanner attached.");
            }
        }
    }
}
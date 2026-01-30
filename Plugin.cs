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
        private const string MyGuid = "com.CaporalCross.LivingPlanetSystem";
        private const string PluginName = "LivingPlanetSystem";
        private const string Version = "1.0.0";

        public static ManualLogSource Log = null;

        public GameObject runner;
        private static bool systemsInitialized = false;
        private static bool eventsHooked = false;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo($"{PluginName} v{Version} is loaded!");

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Aurora")
            {
                if (systemsInitialized)
                {
                    Log.LogInfo("[Plugin] Systems already initialized : skipping.");
                    return;
                }

                systemsInitialized = true;

                runner = new GameObject("RSM_CoreRunner");
                DontDestroyOnLoad(runner);
                runner.AddComponent<RSM_PlayerPositionTracker>();

                RSM_BiomeRegistry.RegisterAllBiomes();

                if (!eventsHooked)
                {
                    RSM_CreatureRegistry.OnCreaturesLoaded += OnCreaturesLoaded;
                    eventsHooked = true;
                }

                RSM_CreatureRegistry.Initialize();
                RSM_CreatureFilter.Initialize();

                if (scene.name == "Cleaner")
                {

                    Log.LogInfo("[Plugin] Cleaner scene detected : shutting down systems.");

                    systemsInitialized = false;

                    if (runner != null)
                    {
                        Destroy(runner);
                        runner = null;
                    }

                    // Clear registries
                    RSM_BiomeRegistry.Clear();
                    RSM_CreatureRegistry.Clear();
                    RSM_CreatureFilter.Clear();

                    // Unhook events
                    if (eventsHooked)
                    {
                        RSM_CreatureRegistry.OnCreaturesLoaded -= OnCreaturesLoaded;
                        eventsHooked = false;
                    }
                }
            }
        }

        private void OnCreaturesLoaded()
        {
            Log.LogInfo("[Plugin] RSM_CreatureRegistry finished scanning creatures.");
        }
    }
}

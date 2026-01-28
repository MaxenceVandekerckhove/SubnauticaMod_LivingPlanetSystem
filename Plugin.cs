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
                GameObject runner = new GameObject("RSM_CoreRunner");
                DontDestroyOnLoad(runner);

                runner.AddComponent<RSM_PlayerPositionTracker>();

                RSM_BiomeRegistry.RegisterAllBiomes();

                // Détecte les créatures et log la fin du scan
                RSM_CreatureRegistry.OnCreaturesLoaded += () =>
                {
                    Plugin.Log.LogInfo("[Plugin] RSM_CreatureRegistry finished scanning creatures.");
                };

                // Lancer la détection des créatures
                RSM_CreatureRegistry.Initialize();
            }
        }
    }
}

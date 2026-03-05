using BepInEx;
using BepInEx.Logging;
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
            Log.LogInfo("[Plugin] Main menu detected — creature scan will start here.");
            // TODO : trigger RSM_CreatureCache check + scan if needed
        }

        private void OnGameWorldLoaded()
        {
            Log.LogInfo("[Plugin] Game world detected — spawn registration will start here.");
            // TODO : trigger RSM_SpawnManager
        }
    }
}

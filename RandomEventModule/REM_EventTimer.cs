using System;
using System.Collections;
using UnityEngine;
using UWE;

namespace LivingPlanetSystem.RandomEventModule
{
    /// <summary>
    /// Drives the random event scheduling loop for the RandomEventModule.
    /// 
    /// Behavior :
    ///   - Started once per game world load via Plugin.OnGameWorldLoaded.
    ///   - Waits a random delay between LPS_Config.EventIntervalMin and
    ///     LPS_Config.EventIntervalMax (in minutes).
    ///   - Calls REM_EventRegistry.FireRandomEvent() when the delay expires.
    ///   - Loops indefinitely until Stop() is called (e.g. on main menu load).
    ///   - Does nothing if the REM module is disabled in config.
    /// </summary>
    public static class REM_EventTimer
    {
        // Private state

        private static bool isRunning = false;
        private static System.Random random;

        // Public API

        /// Starts the event scheduling loop.
        public static void Start()
        {
            if (!LPS_Config.RandomEventEnabled)
            {
                Plugin.Log.LogInfo("[REM_EventTimer] Random Event Module is disabled in config : skipping.");
                return;
            }

            if (isRunning)
            {
                Plugin.Log.LogWarning("[REM_EventTimer] Timer already running : skipping.");
                return;
            }

            random = new System.Random();
            isRunning = true;

            Plugin.Log.LogInfo("[REM_EventTimer] Starting random event timer.");
            CoroutineHost.StartCoroutine(EventLoop());
        }

        /// Stops the event scheduling loop cleanly.
        public static void Stop()
        {
            if (!isRunning)
                return;

            Plugin.Log.LogInfo("[REM_EventTimer] Stopping random event timer.");
            isRunning = false;
        }

        // Private loop

        /// Main coroutine : waits a random interval then fires a random event, indefinitely.
        private static IEnumerator EventLoop()
        {
            while (isRunning)
            {
                float minSeconds = LPS_Config.EventIntervalMin * 60f;
                float maxSeconds = LPS_Config.EventIntervalMax * 60f;
                float delay = minSeconds + (float)random.NextDouble() * (maxSeconds - minSeconds);

                Plugin.Log.LogInfo($"[REM_EventTimer] Next event in {delay / 60f:F1} minutes.");

                float elapsed = 0f;
                while (elapsed < delay && isRunning)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!isRunning)
                    yield break;

                REM_EventRegistry.FireRandomEvent();
            }
        }
    }
}
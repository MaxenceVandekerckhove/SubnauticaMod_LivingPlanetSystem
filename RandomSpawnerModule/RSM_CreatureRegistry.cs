using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UWE;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    /// <summary>
    /// Responsible for scanning all TechTypes and identifying which ones are creatures by loading their prefab and checking for a Creature component.
    /// The scan runs asynchronously to avoid freezing the game.
    /// Results are passed to RSM_CreatureFilter via the OnScanCompleted event.
    /// </summary>
    public static class RSM_CreatureRegistry
    {
        // Private state

        private static readonly List<TechType> scannedCreatures = new List<TechType>();
        private static bool isScanning = false;

        // Events

        /// Passes the raw list of detected creatures to subscribers.
        public static event Action<List<TechType>> OnScanCompleted;

        // Public API

        /// Starts the asynchronous creature scan.
        /// Does nothing if a scan is already running.
        public static void StartScan()
        {
            if (isScanning)
            {
                Plugin.Log.LogWarning("[RSM_CreatureRegistry] Scan already running : skipping.");
                return;
            }

            Plugin.Log.LogInfo("[RSM_CreatureRegistry] Starting creature scan...");

            scannedCreatures.Clear();
            isScanning = true;

            CoroutineHost.StartCoroutine(ScanAllTechTypes());
        }


        /// Returns a copy of the scanned creature list.
        public static List<TechType> GetScannedCreatures()
        {
            return new List<TechType>(scannedCreatures);
        }

        /// Clears all scan results and resets state.
        public static void Clear()
        {
            Plugin.Log.LogInfo("[RSM_CreatureRegistry] Clearing creature registry.");
            scannedCreatures.Clear();
            isScanning = false;
            OnScanCompleted = null;
        }

        // Private scan logic

        /// Main coroutine : launches all TechType checks in parallel, then waits for every single one to complete before declaring the scan done.
        private static IEnumerator ScanAllTechTypes()
        {
            int pendingTasks = 0;
            int totalChecked = 0;

            // Launch all checks in parallel — no yield inside the loop
            // to ensure pendingTasks is fully incremented before any callback fires
            foreach (TechType techType in Enum.GetValues(typeof(TechType)))
            {
                if (techType == TechType.None)
                    continue;

                totalChecked++;
                pendingTasks++;

                CoroutineHost.StartCoroutine(CheckWithTimeout(techType, 5f, isCreature =>
                {
                    if (isCreature)
                        scannedCreatures.Add(techType);

                    pendingTasks--;
                }));
            }

            Plugin.Log.LogInfo($"[RSM_CreatureRegistry] {totalChecked} TechTypes queued for checking.");

            // Wait for all parallel checks to finish, logging progress every 5 seconds
            float logTimer = 0f;
            while (pendingTasks > 0)
            {
                logTimer += Time.deltaTime;

                if (logTimer >= 5f)
                {
                    logTimer = 0f;
                    Plugin.Log.LogInfo($"[RSM_CreatureRegistry] Still scanning... {pendingTasks} tasks remaining.");
                }

                yield return null;
            }

            Plugin.Log.LogInfo($"[RSM_CreatureRegistry] Scan complete : " +
                               $"{scannedCreatures.Count} creatures found out of {totalChecked} TechTypes checked.");

            isScanning = false;

            // Notify subscribers — RSM_CreatureFilter will be listening
            OnScanCompleted?.Invoke(new List<TechType>(scannedCreatures));
        }

        /// Wraps the prefab check with a timeout to avoid hanging on TechTypes whose prefab never loads.
        private static IEnumerator CheckWithTimeout(TechType techType, float timeout, Action<bool> callback)
        {
            bool completed = false;

            CoroutineHost.StartCoroutine(CheckIsTechTypeCreature(techType, result =>
            {
                if (!completed)
                {
                    completed = true;
                    callback?.Invoke(result);
                }
            }));

            // Wait until completed or timeout reached
            float startTime = Time.time;
            while (!completed && Time.time - startTime < timeout)
                yield return null;

            // If the check timed out, treat as non-creature and log a warning
            if (!completed)
            {
                Plugin.Log.LogWarning($"[RSM_CreatureRegistry] Timeout reached for TechType : {techType} : skipping.");
                callback?.Invoke(false);
            }
        }

        /// Loads the prefab for a TechType asynchronously and checks whether it has a Creature component attached.
        private static IEnumerator CheckIsTechTypeCreature(TechType techType, Action<bool> callback)
        {
            var task = CraftData.GetPrefabForTechTypeAsync(techType, verbose: false);
            yield return task;

            GameObject prefab = task.GetResult();
            bool isCreature = prefab != null && prefab.GetComponent<Creature>() != null;

            callback?.Invoke(isCreature);
        }
    }
}
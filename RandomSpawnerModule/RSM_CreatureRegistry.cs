using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UWE;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    public static class RSM_CreatureRegistry
    {
        // Internal list of detected creatures
        private static readonly List<TechType> allCreatures = new List<TechType>();

        // Event triggered when the scan is complete
        public static event Action OnCreaturesLoaded;

        private static bool isScanning = false;
        private static bool cancelScan = false;

        /// Initializes the creature scan.
        public static void Initialize()
        {
            if (isScanning)
            {
                Plugin.Log.LogWarning("[RSM_CreatureRegistry] Scan already running. Skipping.");
                return;
            }

            isScanning = true;
            allCreatures.Clear();
            cancelScan = false;

            CoroutineHost.StartCoroutine(ScanAllCreatures());
        }

        /// Asynchronous scan of all creatures
        private static IEnumerator ScanAllCreatures()
        {
            int pendingTasks = 0;

            foreach (TechType techType in Enum.GetValues(typeof(TechType)))
            {
                if (cancelScan)
                    yield break;

                if (techType == TechType.None)
                    continue;

                pendingTasks++;
                CoroutineHost.StartCoroutine(IsCreatureWithTimeout(techType, 2f, isCreature =>
                {
                    try
                    {
                        if (isCreature)
                        {
                            allCreatures.Add(techType);
                        }
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogError($"[RSM_CreatureRegistry] Error checking techType : {techType} : {e}");
                    }
                    finally
                    {
                        pendingTasks--;
                    }
                }));
            }

            // Wait until all tasks are finished.
            while (pendingTasks > 0)
                yield return null;

            // Final log of all creatures detected
            Plugin.Log.LogInfo($"[RSM_CreatureRegistry] End of creature scan | Creature count : {allCreatures.Count}");
            if (allCreatures.Count > 0)
            {
                string creatureList = string.Join(", ", allCreatures);
                Plugin.Log.LogInfo($"[RSM_CreatureRegistry] Creature list : {creatureList}");
            }

            OnCreaturesLoaded?.Invoke();
            isScanning = false;
        }

        /// Checks if the TechType is a creature with a timeout
        private static IEnumerator IsCreatureWithTimeout(TechType techType, float timeout, Action<bool> callback)
        {
            bool completed = false;

            CoroutineHost.StartCoroutine(IsCreature(techType, result =>
            {
                if (!completed)
                {
                    completed = true;
                    callback?.Invoke(result);
                }
            }));

            float startTime = Time.time;
            while (!completed && Time.time - startTime < timeout)
            {
                if (cancelScan)
                    yield break;

                yield return null;
            }

            if (!completed)
            {
                callback?.Invoke(false);
            }
        }

        /// Load the prefab and check if it's a creature
        private static IEnumerator IsCreature(TechType techType, Action<bool> callback)
        {
            var task = CraftData.GetPrefabForTechTypeAsync(techType, verbose: false);
            yield return task;

            GameObject prefab = task.GetResult();
            bool isCreature = prefab != null && prefab.GetComponent<Creature>() != null;
            callback?.Invoke(isCreature);
        }

        /// Returns a copy of all detected creatures
        public static List<TechType> GetAllCreatures()
        {
            return new List<TechType>(allCreatures);
        }

        // Clear allCreatures list
        public static void Clear()
        {
            Plugin.Log.LogInfo("[RSM_CreatureRegistry] Clearing creature registry.");

            isScanning = false;
            OnCreaturesLoaded = null;
            cancelScan = true;

            allCreatures.Clear();
        }
    }
}

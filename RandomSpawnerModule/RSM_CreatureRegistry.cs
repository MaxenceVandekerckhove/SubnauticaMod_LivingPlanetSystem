using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UWE;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    public static class RSM_CreatureRegistry
    {
        // Liste interne des créatures détectées
        private static readonly List<TechType> allCreatures = new List<TechType>();

        // Event déclenché quand le scan est terminé
        public static event Action OnCreaturesLoaded;

        /// <summary>
        /// Initialise le scan des créatures.
        /// </summary>
        public static void Initialize()
        {
            allCreatures.Clear();
            Plugin.Log.LogInfo($"[RSM_CreatureRegistry] Cleaning creature list | Count = {allCreatures.Count}");
            Plugin.Log.LogInfo($"[RSM_CreatureRegistry] Starting creature scan...");

            CoroutineHost.StartCoroutine(ScanAllCreatures());
        }

        /// <summary>
        /// Scan asynchrone de toutes les créatures
        /// </summary>
        private static IEnumerator ScanAllCreatures()
        {
            int pendingTasks = 0;

            foreach (TechType techType in Enum.GetValues(typeof(TechType)))
            {
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

            // Attendre que toutes les tâches se terminent
            while (pendingTasks > 0)
                yield return null;

            // Log final listant toutes les créatures détectées
            Plugin.Log.LogInfo($"[RSM_CreatureRegistry] End of creature scan | Creature count : {allCreatures.Count}");
            if (allCreatures.Count > 0)
            {
                string creatureList = string.Join(", ", allCreatures);
                Plugin.Log.LogInfo($"[RSM_CreatureRegistry] Creature list : {creatureList}");
            }

            OnCreaturesLoaded?.Invoke();
        }

        /// <summary>
        /// Vérifie si le TechType est une créature avec timeout
        /// </summary>
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
                yield return null;

            if (!completed)
            {
                Plugin.Log.LogWarning($"[RSM_CreatureRegistry] Timeout for {techType}. Ignored.");
                callback?.Invoke(false);
            }
        }

        /// <summary>
        /// Charge le prefab et vérifie si c'est une créature
        /// </summary>
        private static IEnumerator IsCreature(TechType techType, Action<bool> callback)
        {
            var task = CraftData.GetPrefabForTechTypeAsync(techType, verbose: false);
            yield return task;

            GameObject prefab = task.GetResult();
            bool isCreature = prefab != null && prefab.GetComponent<Creature>() != null;
            callback?.Invoke(isCreature);
        }

        /// <summary>
        /// Retourne une copie de toutes les créatures détectées
        /// </summary>
        public static List<TechType> GetAllCreatures()
        {
            return new List<TechType>(allCreatures);
        }
    }
}

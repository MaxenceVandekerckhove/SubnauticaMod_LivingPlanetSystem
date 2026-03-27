using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LivingPlanetSystem.RandomEventModule.Events.Migration
{
    /// <summary>
    /// Responsible for driving all instances of a migration swarm toward their destination.
    ///
    /// Each instance swims independently via SwimBehaviour.SwimTo
    /// Juveniles receive a reduced scale applied once at spawn time.
    ///
    /// Behaviours disabled during migration :
    ///   - SwimRandom              : prevents random wandering
    ///   - StayAtLeashPosition     : prevents return to spawn point
    ///   - FleeOnDamage            : prevents trajectory deviation if attacked
    ///   - FleeWhenScared          : prevents fear-based interruption
    ///   - AggressiveWhenSeeTarget : prevents creatures from attacking the player
    ///
    /// All disabled behaviours are re-enabled when the loop ends.
    ///
    /// Player proximity aggro (Medium and Large categories only) :
    ///   - If the player enters within PlayerAggroRadius of an instance, that instance
    ///     temporarily leaves the swarm : AggressiveWhenSeeTarget is re-enabled and
    ///     SwimTo is no longer called on it.
    ///   - After AgroReturnDelay seconds AND once the player has moved beyond
    ///     PlayerAggroRadius, the instance rejoins the swarm and resumes its migration.
    ///
    /// The loop ends when :
    ///   - All instances have arrived within ArrivalRadius of the destination.
    ///   - The global timeout is reached.
    ///
    /// On arrival or timeout, all surviving instances are released to vanilla AI :
    ///   - All disabled behaviours are re-enabled.
    ///   - leashPosition is updated to the instance's current position.
    ///
    /// Destroyed instances are removed from tracking silently without ending the event early.
    /// </summary>
    public static class REM_SwarmLoop
    {
        // Constants

        // Distance in metres at which an instance is considered to have arrived.
        private const float ArrivalRadius = 20f;

        // Maximum duration of the migration in seconds before all instances are released.
        private const float SwarmTimeout = 300f;

        // Interval between SwimTo calls.
        private const float SwimRefreshInterval = 0.1f;

        // Scale multiplier applied to juvenile instances.
        private const float JuvenileScaleMultiplier = 0.45f;

        // Distance at which a Medium/Large creature temporarily breaks off to attack the player.
        private const float PlayerAggroRadius = 15f;

        // Time in seconds an agro instance must wait before being eligible to rejoin the swarm.
        private const float AgroReturnDelay = 10f;

        // Swim velocity per category

        private const float SwimVelocityLarge = 4f;
        private const float SwimVelocityMedium = 4f;
        private const float SwimVelocitySmall = 2f;

        // Public API

        // Starts the swarm loop for the given composition.
        public static IEnumerator Run(
            List<GameObject> adultInstances,
            List<GameObject> juvenileInstances,
            Vector3 destination,
            REM_MigrationCategory category)
        {
            float swimVelocity = GetSwimVelocity(category);
            bool supportsAggro = category == REM_MigrationCategory.Medium ||
                                  category == REM_MigrationCategory.Large;

            // Apply juvenile scale once at start
            ApplyJuvenileScale(juvenileInstances);

            // Merge all instances into one tracking list
            List<GameObject> allInstances = new List<GameObject>(adultInstances);
            allInstances.AddRange(juvenileInstances);

            // Disable all behaviours that could interfere with migration
            foreach (GameObject instance in allInstances)
                DisableMigrationInterferingBehaviours(instance);

            // Tracks instances that have temporarily broken off to attack the player.
            Dictionary<GameObject, float> agroInstances = new Dictionary<GameObject, float>();

            Plugin.Log.LogInfo($"[REM_SwarmLoop] Migration started : " +
                               $"{adultInstances.Count} adult(s), " +
                               $"{juvenileInstances.Count} juvenile(s). " +
                               $"Category : {category} : velocity : {swimVelocity}. " +
                               $"Destination : {destination}. " +
                               $"Timeout in {SwarmTimeout}s.");

            float elapsed = 0f;
            float refreshTimer = 0f;

            GameObject player = Player.main != null ? Player.main.gameObject : null;

            while (elapsed < SwarmTimeout)
            {
                // Remove destroyed instances silently
                allInstances.RemoveAll(i => i == null);
                RemoveNullKeys(agroInstances);

                if (allInstances.Count == 0)
                {
                    Plugin.Log.LogInfo("[REM_SwarmLoop] All instances destroyed or arrived : event ended.");
                    yield break;
                }

                if (AllArrived(allInstances, destination))
                {
                    Plugin.Log.LogInfo("[REM_SwarmLoop] All instances reached destination : releasing to vanilla AI.");
                    ReleaseAll(allInstances);
                    yield break;
                }

                // Player proximity aggro (Medium / Large only)
                if (supportsAggro && player != null)
                {
                    Vector3 playerPos = player.transform.position;

                    foreach (GameObject instance in allInstances)
                    {
                        if (instance == null)
                            continue;

                        float dist = Vector3.Distance(instance.transform.position, playerPos);

                        if (!agroInstances.ContainsKey(instance))
                        {
                            // Instance is currently in the swarm — check if player is too close
                            if (dist <= PlayerAggroRadius)
                            {
                                EnterAgroMode(instance);
                                agroInstances[instance] = 0f;

                                Plugin.Log.LogDebug($"[REM_SwarmLoop] {instance.name} broke off to attack player " +
                                                    $"(distance={dist:F1}m).");
                            }
                        }
                        else
                        {
                            // Instance is in aggro mode — increment its timer
                            agroInstances[instance] += Time.deltaTime;

                            // Rejoin swarm once the delay has elapsed AND the player is far enough
                            if (agroInstances[instance] >= AgroReturnDelay && dist > PlayerAggroRadius)
                            {
                                agroInstances.Remove(instance);
                                ExitAgroMode(instance);

                                Plugin.Log.LogDebug($"[REM_SwarmLoop] {instance.name} rejoined swarm " +
                                                    $"after {AgroReturnDelay}s (distance={dist:F1}m).");
                            }
                        }
                    }
                }

                // SwimTo refresh
                refreshTimer += Time.deltaTime;
                if (refreshTimer >= SwimRefreshInterval)
                {
                    refreshTimer = 0f;
                    RefreshSwimTo(allInstances, agroInstances, destination, swimVelocity);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Timeout reached
            Plugin.Log.LogInfo($"[REM_SwarmLoop] Timeout reached after {SwarmTimeout}s : " +
                               $"releasing {allInstances.Count} surviving instance(s) to vanilla AI.");

            allInstances.RemoveAll(i => i == null);
            ReleaseAll(allInstances);
        }

        // Private helpers

        // Returns the swim velocity appropriate for the migration category.
        private static float GetSwimVelocity(REM_MigrationCategory category)
        {
            switch (category)
            {
                case REM_MigrationCategory.Large: return SwimVelocityLarge;
                case REM_MigrationCategory.Medium: return SwimVelocityMedium;
                default: return SwimVelocitySmall;
            }
        }

        // Applies a reduced scale to all juvenile instances.
        private static void ApplyJuvenileScale(List<GameObject> juveniles)
        {
            foreach (GameObject juvenile in juveniles)
            {
                if (juvenile == null)
                    continue;

                juvenile.transform.localScale *= JuvenileScaleMultiplier;

                Plugin.Log.LogDebug($"[REM_SwarmLoop] Juvenile scale applied to {juvenile.name} " +
                                    $"(scale={juvenile.transform.localScale}).");
            }
        }

        // Returns true if all surviving instances are within ArrivalRadius of the destination.
        private static bool AllArrived(List<GameObject> instances, Vector3 destination)
        {
            foreach (GameObject instance in instances)
            {
                if (instance == null)
                    continue;

                if (Vector3.Distance(instance.transform.position, destination) > ArrivalRadius)
                    return false;
            }

            return true;
        }

        // Calls SwimTo on each surviving instance that is NOT currently in aggro mode.
        private static void RefreshSwimTo(List<GameObject> instances,
                                          Dictionary<GameObject, float> agroInstances,
                                          Vector3 destination,
                                          float velocity)
        {
            foreach (GameObject instance in instances)
            {
                if (instance == null)
                    continue;

                // Skip instances that have broken off to attack the player
                if (agroInstances.ContainsKey(instance))
                    continue;

                SwimBehaviour swimBehaviour = instance.GetComponent<SwimBehaviour>();

                if (swimBehaviour == null)
                    continue;

                try
                {
                    Vector3 direction = (destination - instance.transform.position).normalized;
                    swimBehaviour.SwimTo(destination, direction, velocity);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[REM_SwarmLoop] SwimTo failed on {instance.name} : {e.Message}");
                }
            }
        }

        // Re-enables AggressiveWhenSeeTarget so the creature can attack the player.
        private static void EnterAgroMode(GameObject instance)
        {
            if (instance == null)
                return;

            foreach (AggressiveWhenSeeTarget c in instance.GetComponentsInChildren<AggressiveWhenSeeTarget>(includeInactive: true))
                c.enabled = true;
        }

        // Disables AggressiveWhenSeeTarget again so the creature resumes its migration.
        private static void ExitAgroMode(GameObject instance)
        {
            if (instance == null)
                return;

            foreach (AggressiveWhenSeeTarget c in instance.GetComponentsInChildren<AggressiveWhenSeeTarget>(includeInactive: true))
                c.enabled = false;
        }

        // Disables all behaviours that could interfere with migration movement.
        private static void DisableMigrationInterferingBehaviours(GameObject instance)
        {
            if (instance == null)
                return;

            foreach (SwimRandom c in instance.GetComponentsInChildren<SwimRandom>(includeInactive: true))
                c.enabled = false;

            foreach (StayAtLeashPosition c in instance.GetComponentsInChildren<StayAtLeashPosition>(includeInactive: true))
                c.enabled = false;

            foreach (FleeOnDamage c in instance.GetComponentsInChildren<FleeOnDamage>(includeInactive: true))
                c.enabled = false;

            foreach (FleeWhenScared c in instance.GetComponentsInChildren<FleeWhenScared>(includeInactive: true))
                c.enabled = false;

            foreach (AggressiveWhenSeeTarget c in instance.GetComponentsInChildren<AggressiveWhenSeeTarget>(includeInactive: true))
                c.enabled = false;

            Plugin.Log.LogDebug($"[REM_SwarmLoop] Migration behaviours disabled on {instance.name}.");
        }

        // Re-enables all previously disabled behaviours and releases instances to vanilla AI.
        private static void ReleaseAll(List<GameObject> instances)
        {
            foreach (GameObject instance in instances)
            {
                if (instance == null)
                    continue;

                Creature creature = instance.GetComponent<Creature>();
                if (creature != null)
                    creature.leashPosition = instance.transform.position;

                foreach (SwimRandom c in instance.GetComponentsInChildren<SwimRandom>(includeInactive: true))
                    c.enabled = true;

                foreach (StayAtLeashPosition c in instance.GetComponentsInChildren<StayAtLeashPosition>(includeInactive: true))
                    c.enabled = true;

                foreach (FleeOnDamage c in instance.GetComponentsInChildren<FleeOnDamage>(includeInactive: true))
                    c.enabled = true;

                foreach (FleeWhenScared c in instance.GetComponentsInChildren<FleeWhenScared>(includeInactive: true))
                    c.enabled = true;

                foreach (AggressiveWhenSeeTarget c in instance.GetComponentsInChildren<AggressiveWhenSeeTarget>(includeInactive: true))
                    c.enabled = true;

                Plugin.Log.LogDebug($"[REM_SwarmLoop] {instance.name} released to vanilla AI.");
            }
        }

        // Removes null keys from the agro tracking dictionary (destroyed instances).
        private static void RemoveNullKeys(Dictionary<GameObject, float> dict)
        {
            var toRemove = new List<GameObject>();

            foreach (GameObject key in dict.Keys)
            {
                if (key == null)
                    toRemove.Add(key);
            }

            foreach (GameObject key in toRemove)
                dict.Remove(key);
        }
    }
}
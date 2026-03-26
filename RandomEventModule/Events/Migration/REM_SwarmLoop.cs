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
    /// The loop ends when :
    ///   - All instances have arrived within ArrivalRadius of the destination.
    ///   - The global timeout is reached.
    ///
    /// On arrival or timeout, all surviving instances are released to vanilla AI :
    ///   - All disabled behaviours are re-enabled.
    ///   - leashPosition is updated to the instance's current position.
    ///
    /// Destroyed instances are removed from tracking silently without ending the event early.
    ///
    /// --- SwimBehaviour.SwimToInternal timing constraint ---
    /// SwimToInternal sets overridingTarget = true for 0.2s on TurnAround and 0.5s
    /// on Overshoot. While overridingTarget is active, any subsequent SwimTo call
    /// returns immediately without effect. Calling SwimTo faster than the longest
    /// override duration (0.5s) therefore produces a cycle where the creature
    /// perpetually re-enters TurnAround or Overshoot and never advances — this is
    /// the root cause of small creatures appearing stationary during migration.
    /// SwimRefreshInterval must be strictly greater than 0.5s to guarantee each
    /// SwimTo call lands after the previous override has fully expired.
    /// </summary>
    public static class REM_SwarmLoop
    {
        // Constants

        /// Distance in metres at which an instance is considered to have arrived.
        private const float ArrivalRadius = 20f;

        /// Maximum duration of the migration in seconds before all instances are released.
        private const float SwarmTimeout = 300f;

        /// Interval between SwimTo calls.
        /// Must be strictly greater than SwimBehaviour's longest internal override duration
        /// (Overshoot = 0.5s) to prevent new SwimTo calls from being silently dropped
        /// while overridingTarget is still active.
        private const float SwimRefreshInterval = 0.1f;

        /// Scale multiplier applied to juvenile instances.
        private const float JuvenileScaleMultiplier = 0.45f;

        // Swim velocity per category

        private const float SwimVelocityLarge = 4f;
        private const float SwimVelocityMedium = 4f;
        private const float SwimVelocitySmall = 4f;

        // Public API

        /// Starts the swarm loop for the given composition.
        public static IEnumerator Run(
            List<GameObject> adultInstances,
            List<GameObject> juvenileInstances,
            Vector3 destination,
            REM_MigrationCategory category)
        {
            float swimVelocity = GetSwimVelocity(category);

            // Apply juvenile scale once at start
            ApplyJuvenileScale(juvenileInstances);

            // Merge all instances into one tracking list
            List<GameObject> allInstances = new List<GameObject>(adultInstances);
            allInstances.AddRange(juvenileInstances);

            // Disable all behaviours that could interfere with migration
            foreach (GameObject instance in allInstances)
                DisableMigrationInterferingBehaviours(instance);

            Plugin.Log.LogInfo($"[REM_SwarmLoop] Migration started : " +
                               $"{adultInstances.Count} adult(s), " +
                               $"{juvenileInstances.Count} juvenile(s). " +
                               $"Category : {category} : velocity : {swimVelocity}. " +
                               $"Destination : {destination}. " +
                               $"Timeout in {SwarmTimeout}s.");

            float elapsed = 0f;
            float refreshTimer = 0f;

            while (elapsed < SwarmTimeout)
            {
                // Remove destroyed instances silently
                allInstances.RemoveAll(i => i == null);

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

                // Refresh SwimTo at an interval that guarantees the previous
                // SwimBehaviour override (TurnAround 0.2s / Overshoot 0.5s) has
                // fully expired before we issue the next command.
                refreshTimer += Time.deltaTime;
                if (refreshTimer >= SwimRefreshInterval)
                {
                    refreshTimer = 0f;
                    RefreshSwimTo(allInstances, destination, swimVelocity);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Timeout reached — release survivors to vanilla AI
            Plugin.Log.LogInfo($"[REM_SwarmLoop] Timeout reached after {SwarmTimeout}s : " +
                               $"releasing {allInstances.Count} surviving instance(s) to vanilla AI.");

            allInstances.RemoveAll(i => i == null);
            ReleaseAll(allInstances);
        }

        // Private helpers

        /// Returns the swim velocity appropriate for the migration category.
        private static float GetSwimVelocity(REM_MigrationCategory category)
        {
            switch (category)
            {
                case REM_MigrationCategory.Large: return SwimVelocityLarge;
                case REM_MigrationCategory.Medium: return SwimVelocityMedium;
                default: return SwimVelocitySmall;
            }
        }

        /// Applies a reduced scale to all juvenile instances.
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

        /// Returns true if all surviving instances are within ArrivalRadius of the destination.
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

        /// Calls SwimTo on each surviving instance with a full 3D direction toward the destination.
        private static void RefreshSwimTo(List<GameObject> instances, Vector3 destination, float velocity)
        {
            foreach (GameObject instance in instances)
            {
                if (instance == null)
                    continue;

                SwimBehaviour swimBehaviour = instance.GetComponent<SwimBehaviour>();

                if (swimBehaviour == null)
                    continue;

                try
                {
                    // Compute full 3D direction including Y component
                    Vector3 direction = (destination - instance.transform.position).normalized;
                    swimBehaviour.SwimTo(destination, direction, velocity);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[REM_SwarmLoop] SwimTo failed on {instance.name} : {e.Message}");
                }
            }
        }

        /// Disables all behaviours that could interfere with migration movement.
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

        /// Re-enables all previously disabled behaviours and releases instances to vanilla AI.
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
    }
}
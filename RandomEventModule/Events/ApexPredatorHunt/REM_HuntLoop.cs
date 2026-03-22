using System;
using System.Collections;
using UnityEngine;

namespace LivingPlanetSystem.RandomEventModule.Events.ApexPredatorHunt
{
    /// <summary>
    /// Responsible for driving the hunt behaviour once the apex predator is spawned.
    /// 
    /// Responsibilities :
    ///   - Disables SwimRandom and LeashPosition so the predator does not drift
    ///     back to its spawn point.
    ///   - Re-applies aggro toward the player every AggroRefreshInterval seconds
    ///     to prevent natural de-aggro.
    ///   - Monitors three end conditions every frame :
    ///       1. Creature destroyed naturally  → event ends cleanly.
    ///       2. Creature within ArrivalRadius → wander behaviours restored, event ends.
    ///       3. Timeout reached               → creature despawned, event ends.
    /// </summary>
    public static class REM_HuntLoop
    {
        // Constants

        /// Distance in metres at which the predator is considered to have reached the player.
        private const float ArrivalRadius = 25f;

        /// Maximum duration of the hunt in seconds before the predator is despawned.
        private const float HuntTimeout = 180f;

        /// Interval in seconds between aggro refresh calls.
        private const float AggroRefreshInterval = 0.1f;

        // Public API

        /// Starts the hunt loop for the given predator instance.
        /// Assumes the instance has already been spawned and activated.
        public static IEnumerator Run(GameObject instance)
        {
            if (instance == null)
            {
                Plugin.Log.LogWarning("[REM_HuntLoop] Instance is null : aborting.");
                yield break;
            }

            GameObject player = Player.main?.gameObject;

            if (player == null)
            {
                Plugin.Log.LogWarning("[REM_HuntLoop] Player not found : aborting.");
                UnityEngine.Object.Destroy(instance);
                yield break;
            }

            DisableWanderBehaviours(instance);

            float elapsed = 0f;
            float aggroTimer = 0f;

            Plugin.Log.LogInfo($"[REM_HuntLoop] Hunt started for {instance.name}. " +
                               $"Timeout in {HuntTimeout}s.");

            while (elapsed < HuntTimeout)
            {
                // End condition 1 : creature destroyed naturally
                if (instance == null)
                {
                    Plugin.Log.LogInfo("[REM_HuntLoop] Predator was destroyed naturally : event ended.");
                    UnityEngine.Object.Destroy(instance);
                    yield break;
                }

                float dist = Vector3.Distance(instance.transform.position, player.transform.position);

                // End condition 2 : predator reached the player
                if (dist <= ArrivalRadius)
                {
                    Plugin.Log.LogInfo($"[REM_HuntLoop] Predator reached the player (dist={dist:F1}m) : " +
                                       $"releasing to vanilla AI.");
                    RestoreWanderBehaviours(instance);
                    yield break;
                }

                // Aggro refresh
                aggroTimer += Time.deltaTime;
                if (aggroTimer >= AggroRefreshInterval)
                {
                    aggroTimer = 0f;
                    ApplyAggro(instance, player);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // End condition 3 : timeout
            Plugin.Log.LogInfo($"[REM_HuntLoop] Hunt timed out after {HuntTimeout}s : despawning predator.");

            if (instance != null)
                UnityEngine.Object.Destroy(instance);
        }

        // Private helpers

        /// Forces the creature to hunt the player by :
        ///   1. Setting Aggression to maximum so AttackLastTarget.Evaluate() passes its threshold.
        ///   2. Setting the LastTarget on all aggressive components to the player.
        ///   3. Extending rememberTargetTime on AttackLastTarget so the target is not
        ///      forgotten between aggro refreshes.
        ///   4. Directly calling SwimBehaviour.Attack() toward the player position to
        ///      bypass CanBeAttacked() checks that block aggro when the player is in a base.
        private static void ApplyAggro(GameObject instance, GameObject player)
        {
            if (instance == null || player == null)
                return;

            // Force aggression to maximum on the Creature component
            Creature creature = instance.GetComponent<Creature>();
            if (creature != null)
                creature.Aggression.Value = 1f;

            // Set target on all AggressiveWhenSeeTarget components
            var aggressors = instance.GetComponentsInChildren<AggressiveWhenSeeTarget>(includeInactive: false);
            foreach (AggressiveWhenSeeTarget aggressor in aggressors)
            {
                try { aggressor.lastTarget.SetTarget(player); }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[REM_HuntLoop] AggressiveWhenSeeTarget aggro failed : {e.Message}");
                }
            }

            // Set target on all AggressiveToPilotingVehicle components
            var vehicleAggressors = instance.GetComponentsInChildren<AggressiveToPilotingVehicle>(includeInactive: false);
            foreach (AggressiveToPilotingVehicle aggressor in vehicleAggressors)
            {
                try { aggressor.lastTarget.SetTarget(player); }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[REM_HuntLoop] AggressiveToPilotingVehicle aggro failed : {e.Message}");
                }
            }

            // Keep rememberTargetTime high enough so the target survives until the next refresh
            var attackActions = instance.GetComponentsInChildren<AttackLastTarget>(includeInactive: false);
            foreach (AttackLastTarget attack in attackActions)
            {
                try { attack.rememberTargetTime = AggroRefreshInterval * 10f; }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[REM_HuntLoop] AttackLastTarget.rememberTargetTime failed : {e.Message}");
                }
            }

            // Force swim toward player directly
            SwimBehaviour swimBehaviour = instance.GetComponent<SwimBehaviour>();
            if (swimBehaviour != null)
            {
                try
                {
                    Vector3 toPlayer = player.transform.position - instance.transform.position;
                    swimBehaviour.Attack(player.transform.position, toPlayer.normalized, swimBehaviour.turnSpeed * 10f);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[REM_HuntLoop] SwimBehaviour.Attack failed : {e.Message}");
                }
            }
        }

        /// Disables SwimRandom and StayAtLeashPosition to prevent the predator from wandering.
        private static void DisableWanderBehaviours(GameObject instance)
        {
            foreach (SwimRandom c in instance.GetComponentsInChildren<SwimRandom>(includeInactive: true))
                c.enabled = false;

            foreach (StayAtLeashPosition c in instance.GetComponentsInChildren<StayAtLeashPosition>(includeInactive: true))
                c.enabled = false;

            Plugin.Log.LogDebug($"[REM_HuntLoop] Wander behaviours disabled on {instance.name}.");
        }

        /// Re-enables SwimRandom and StayAtLeashPosition when the hunt ends naturally.
        /// Updates creature.leashPosition to the creature's current position so it stays in the player's area rather than returning to its original spawn point.
        private static void RestoreWanderBehaviours(GameObject instance)
        {
            if (instance == null)
                return;

            // Update leash position to current location before re-enabling StayAtLeashPosition
            Creature creature = instance.GetComponent<Creature>();
            if (creature != null)
                creature.leashPosition = instance.transform.position;

            foreach (SwimRandom c in instance.GetComponentsInChildren<SwimRandom>(includeInactive: true))
                c.enabled = true;

            foreach (StayAtLeashPosition c in instance.GetComponentsInChildren<StayAtLeashPosition>(includeInactive: true))
                c.enabled = true;

            Plugin.Log.LogDebug($"[REM_HuntLoop] Wander behaviours restored on {instance.name}.");
        }
    }
}
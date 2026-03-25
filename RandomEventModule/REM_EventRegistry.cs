using LivingPlanetSystem.RandomEventModule.Events.ApexPredatorHunt;
using LivingPlanetSystem.RandomEventModule.Events.Migration;
using System;
using System.Collections.Generic;

namespace LivingPlanetSystem.RandomEventModule
{
    /// <summary>
    /// Central registry for all random events in the RandomEventModule.
    /// 
    /// Responsibilities :
    ///   - Holds the list of all registered REM_EventBase implementations.
    ///   - Filters to enabled events only at selection time.
    ///   - Selects one event using weighted random sampling.
    ///   - Calls Trigger() on the selected event.
    ///   
    /// </summary>
    public static class REM_EventRegistry
    {
        // Private state

        private static readonly List<REM_EventBase> registeredEvents = new List<REM_EventBase>();
        private static Random random;

        // Public API

        /// Registers all known random events and initializes the random instance.
        public static void Initialize()
        {
            registeredEvents.Clear();
            random = new Random();

            registeredEvents.Add(new REM_ApexPredatorHunt());
            registeredEvents.Add(new REM_MigrationSmall());
            registeredEvents.Add(new REM_MigrationMedium());
            registeredEvents.Add(new REM_MigrationLarge());

            Plugin.Log.LogInfo($"[REM_EventRegistry] {registeredEvents.Count} event(s) registered.");
        }

        /// Selects a random enabled event via weighted sampling and triggers it.
        public static void FireRandomEvent()
        {
            List<REM_EventBase> enabled = new List<REM_EventBase>();

            foreach (REM_EventBase ev in registeredEvents)
            {
                if (ev.IsEnabled)
                    enabled.Add(ev);
            }

            if (enabled.Count == 0)
            {
                Plugin.Log.LogWarning("[REM_EventRegistry] No enabled events available : skipping.");
                return;
            }

            REM_EventBase selected = WeightedRandom(enabled);
            Plugin.Log.LogInfo($"[REM_EventRegistry] Selected event : {selected.EventId}");

            selected.Trigger();
        }

        // Private helpers

        /// Selects one event from the list using weighted random sampling.
        private static REM_EventBase WeightedRandom(List<REM_EventBase> events)
        {
            float totalWeight = 0f;
            foreach (REM_EventBase ev in events)
                totalWeight += ev.Weight;

            float roll = (float)random.NextDouble() * totalWeight;
            float cumulative = 0f;

            foreach (REM_EventBase ev in events)
            {
                cumulative += ev.Weight;
                if (roll <= cumulative)
                    return ev;
            }

            // Fallback — should never be reached
            return events[events.Count - 1];
        }
    }
}
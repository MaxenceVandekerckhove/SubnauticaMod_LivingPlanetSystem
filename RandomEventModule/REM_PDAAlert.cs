using FMODUnity;
using LivingPlanetSystem.Core;
using LivingPlanetSystem.RandomEventModule.Events.Migration;
using UnityEngine;

namespace LivingPlanetSystem.RandomEventModule
{
    /// <summary>
    /// Global utility for displaying PDA alert notifications during random events.
    /// Each event constructs its message via a dedicated static factory method.
    /// Plays a custom FMOD sound event alongside each alert if the audio banks are loaded.
    /// </summary>
    public static class REM_PDAAlert
    {
        // Default display duration in seconds
        private const float DefaultDuration = 6f;

        // Data structure

        // Holds all data needed to display one PDA alert.
        public struct AlertMessage
        {
            // Text displayed on screen and logged
            public string Text;

            // How long the message stays on screen
            public float Duration;

            // FMOD event path to play alongside the alert — empty means no sound
            public string SoundEvent;

            public AlertMessage(string text, float duration = DefaultDuration,
                                string soundEvent = "")
            {
                Text = text;
                Duration = duration;
                SoundEvent = soundEvent;
            }
        }

        // Display

        // Displays the given alert message in-game via ErrorMessage and plays its associated FMOD sound event if available.
        public static void Show(AlertMessage message)
        {
            if (string.IsNullOrEmpty(message.Text))
            {
                Plugin.Log.LogWarning("[REM_PDAAlert] Attempted to show an empty alert : skipping.");
                return;
            }

            Plugin.Log.LogInfo($"[REM_PDAAlert] Displaying alert : \"{message.Text}\"");

            try
            {
                ErrorMessage.AddError(message.Text);

                if (!string.IsNullOrEmpty(message.SoundEvent))
                    LPS_AudioManager.PlaySound(message.SoundEvent);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[REM_PDAAlert] Failed to display alert : {e.Message}");
            }
        }

        // Factory methods

        // Alert for the Apex Predator Hunt event.
        public static AlertMessage ForApexPredatorHunt()
        {
            return new AlertMessage(
                "Biological threat detected. A large predator is approaching your position.",
                duration: 7f,
                soundEvent: LPS_AudioManager.SoundApexPredatorAlert
            );
        }

        // Alert for a Migration event, with message and sound tailored to the creature category.
        public static AlertMessage ForMigration(REM_MigrationCategory category)
        {
            switch (category)
            {
                case REM_MigrationCategory.Small:
                    return new AlertMessage(
                        "Migration detected. A school of small organisms is passing through the area.",
                        duration: 6f,
                        soundEvent: LPS_AudioManager.SoundMigrationSmallAlert
                    );

                case REM_MigrationCategory.Medium:
                    return new AlertMessage(
                        "Migration detected. A group of medium-sized creatures is moving through the area.",
                        duration: 6f,
                        soundEvent: LPS_AudioManager.SoundMigrationMediumAlert
                    );

                case REM_MigrationCategory.Large:
                    return new AlertMessage(
                        "Migration detected. Large organisms are moving through the area. Maintain safe distance.",
                        duration: 8f,
                        soundEvent: LPS_AudioManager.SoundMigrationLargeAlert
                    );

                default:
                    return new AlertMessage(
                        "Migration activity detected in the area.",
                        duration: 6f
                    );
            }
        }
    }
}
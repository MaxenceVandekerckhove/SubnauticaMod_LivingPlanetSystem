using System.IO;
using System.Reflection;
using FMOD;

namespace LivingPlanetSystem.Core
{
    /// <summary>
    /// Responsible for playing custom audio files for the LivingPlanetSystem mod.
    /// Bypasses the FMOD event system entirely by using the FMOD Core API directly,
    /// which avoids the Master Bank GUID matching requirement.
    /// Audio files are loaded from the sounds/ subdirectory of the plugin folder.
    /// </summary>
    public static class LPS_AudioManager
    {
        // Paths

        private static readonly string SoundsDirectory = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "sounds"
        );

        // Sound file paths

        public static string SoundApexPredatorAlert => Path.Combine(SoundsDirectory, "ApexPredatorHuntAlert.wav");
        public static string SoundMigrationLargeAlert => Path.Combine(SoundsDirectory, "LargeCreatureMigration.wav");
        public static string SoundMigrationMediumAlert => Path.Combine(SoundsDirectory, "MediumCreatureMigration.wav");
        public static string SoundMigrationSmallAlert => Path.Combine(SoundsDirectory, "SmallCreatureMigration.wav");

        // Public API

        // Plays a sound file directly via the FMOD Core API.
        public static void PlaySound(string soundFilePath)
        {
            if (!File.Exists(soundFilePath))
            {
                Plugin.Log.LogWarning($"[LPS_AudioManager] Sound file not found : {soundFilePath}");
                return;
            }

            try
            {
                FMODUnity.RuntimeManager.StudioSystem.getCoreSystem(out FMOD.System coreSystem);

                coreSystem.createSound(soundFilePath, MODE.DEFAULT, out Sound sound);
                coreSystem.getMasterChannelGroup(out ChannelGroup masterGroup);
                coreSystem.playSound(sound, masterGroup, false, out Channel channel);

                Plugin.Log.LogInfo($"[LPS_AudioManager] Playing sound : {soundFilePath}");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[LPS_AudioManager] Failed to play sound : {e.Message}");
            }
        }
    }
}
using UWE;

namespace LivingPlanetSystem.RandomEventModule
{
    /// <summary>
    /// Abstract base class for all random events in the RandomEventModule.
    /// </summary>
    public abstract class REM_EventBase
    {
        // Abstract contract
        public abstract string EventId { get; }

        public abstract bool IsEnabled { get; }

        public abstract float Weight { get; }

        protected abstract System.Collections.IEnumerator Execute();

        // Sealed dispatch
        public void Trigger()
        {
            Plugin.Log.LogInfo($"[REM] Event triggered : {EventId}");
            CoroutineHost.StartCoroutine(Execute());
        }
    }
}
using System;
using DisasterSim.Building;

namespace DisasterSim.Core
{
    /// <summary>Central event bus so managers stay decoupled.</summary>
    public static class SimulationEvents
    {
        public static event Action<SimulationState> OnSimulationStateChanged;
        public static event Action<float> OnSimulationTimeChanged;
        public static event Action<float> OnSimulationSpeedChanged;
        public static event Action<string> OnDisasterStarted;
        public static event Action OnDisasterStopped;
        public static event Action OnBuildingReset;
        public static event Action OnEnvironmentReset;
        public static event Action<BuildingComponent> OnComponentSelected;
        public static event Action OnComponentDeselected;
        public static event Action OnDamageStatsUpdated;
        public static event Action OnExperimentSnapshotTaken;

        public static void RaiseSimulationStateChanged(SimulationState state) => OnSimulationStateChanged?.Invoke(state);
        public static void RaiseSimulationTimeChanged(float time) => OnSimulationTimeChanged?.Invoke(time);
        public static void RaiseSimulationSpeedChanged(float speed) => OnSimulationSpeedChanged?.Invoke(speed);
        public static void RaiseDisasterStarted(string disasterName) => OnDisasterStarted?.Invoke(disasterName);
        public static void RaiseDisasterStopped() => OnDisasterStopped?.Invoke();
        public static void RaiseBuildingReset() => OnBuildingReset?.Invoke();
        public static void RaiseEnvironmentReset() => OnEnvironmentReset?.Invoke();
        public static void RaiseComponentSelected(BuildingComponent comp) => OnComponentSelected?.Invoke(comp);
        public static void RaiseComponentDeselected() => OnComponentDeselected?.Invoke();
        public static void RaiseDamageStatsUpdated() => OnDamageStatsUpdated?.Invoke();
        public static void RaiseExperimentSnapshotTaken() => OnExperimentSnapshotTaken?.Invoke();
    }
}

using UnityEngine;
using DisasterSim.Core;

namespace DisasterSim.Core
{
    /// <summary>
    /// Central simulation clock and state machine. Controls play/pause, simulation speed,
    /// and coordinates reset operations across subsystems via SimulationEvents.
    /// </summary>
    public class SimulationManager : MonoBehaviour
    {
        public static SimulationManager Instance { get; private set; }

        [SerializeField] private float[] speedOptions = { 0.25f, 0.5f, 1f, 2f, 4f };

        public SimulationState State { get; private set; } = SimulationState.Idle;
        public float SimulationTime { get; private set; }
        public float SimulationSpeed { get; private set; } = 1f;
        public int SpeedIndex { get; private set; } = 2;
        public string ActiveDisaster { get; private set; } = "NONE";

        private void Awake()
        {
            Instance = this;
            SimulationEvents.OnDisasterStarted += name => ActiveDisaster = name.ToUpperInvariant();
            SimulationEvents.OnDisasterStopped += () => ActiveDisaster = "NONE";
        }

        private void OnDestroy()
        {
            SimulationEvents.OnDisasterStarted -= name => ActiveDisaster = name.ToUpperInvariant();
            SimulationEvents.OnDisasterStopped -= () => ActiveDisaster = "NONE";
        }

        private void Update()
        {
            if (State == SimulationState.Running || State == SimulationState.DisasterActive)
            {
                SimulationTime += Time.deltaTime * SimulationSpeed;
                SimulationEvents.RaiseSimulationTimeChanged(SimulationTime);
            }
        }

        public void StartSimulation()
        {
            State = SimulationState.Running;
            SimulationEvents.RaiseSimulationStateChanged(State);
        }

        public void PauseSimulation()
        {
            if (State == SimulationState.Running || State == SimulationState.DisasterActive)
            {
                State = SimulationState.Paused;
                SimulationEvents.RaiseSimulationStateChanged(State);
            }
        }

        public void ResumeSimulation()
        {
            if (State == SimulationState.Paused)
            {
                State = SimulationState.DisasterActive;
                SimulationEvents.RaiseSimulationStateChanged(State);
            }
        }

        public void SetDisasterActive(bool active)
        {
            if (active && State != SimulationState.Paused)
                State = SimulationState.DisasterActive;
            else if (!active && State == SimulationState.DisasterActive)
                State = SimulationState.Running;
            SimulationEvents.RaiseSimulationStateChanged(State);
        }

        public void CycleSpeed()
        {
            SpeedIndex = (SpeedIndex + 1) % speedOptions.Length;
            SimulationSpeed = speedOptions[SpeedIndex];
            SimulationEvents.RaiseSimulationSpeedChanged(SimulationSpeed);
        }

        public void SetSpeedIndex(int index)
        {
            SpeedIndex = Mathf.Clamp(index, 0, speedOptions.Length - 1);
            SimulationSpeed = speedOptions[SpeedIndex];
            SimulationEvents.RaiseSimulationSpeedChanged(SimulationSpeed);
        }

        public void ResetSimulation()
        {
            SimulationTime = 0f;
            State = SimulationState.Idle;
            ActiveDisaster = "NONE";
            SimulationEvents.RaiseSimulationTimeChanged(0f);
            SimulationEvents.RaiseSimulationStateChanged(State);
            SimulationEvents.RaiseDisasterStopped();
            SimulationEvents.RaiseBuildingReset();
            SimulationEvents.RaiseEnvironmentReset();
        }
    }
}

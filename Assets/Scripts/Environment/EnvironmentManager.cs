using System.Collections.Generic;
using UnityEngine;
using DisasterSim.Building;
using DisasterSim.Core;

namespace DisasterSim.Environment
{
    /// <summary>
    /// Real-time environmental controls. Temperature, humidity, wind and water level
    /// modify material durability and contribute external stress to the structure.
    /// </summary>
    public class EnvironmentManager : MonoBehaviour
    {
        public static EnvironmentManager Instance { get; private set; }

        public float Temperature { get; private set; } = 20f;
        public float Humidity { get; private set; } = 50f;
        public float WindSpeed { get; private set; }
        public float WaterLevel { get; private set; }
        public float Rainfall { get; private set; }

        /// <summary>Thermal stress multiplier from extreme temperatures.</summary>
        public float ThermalStressFactor { get; private set; }
        /// <summary>Corrosion/degradation rate from humidity.</summary>
        public float CorrosionRate { get; private set; }
        /// <summary>Lateral wind force in Newtons applied to exposed surfaces.</summary>
        public float WindForceN { get; private set; }

        private void Awake()
        {
            Instance = this;
            SimulationEvents.OnEnvironmentReset += ResetEnvironment;
            RecalculateFactors();
        }

        private void OnDestroy()
        {
            SimulationEvents.OnEnvironmentReset -= ResetEnvironment;
        }

        public void SetTemperature(float celsius)
        {
            Temperature = Mathf.Clamp(celsius, -30f, 60f);
            RecalculateFactors();
        }

        public void SetHumidity(float percent)
        {
            Humidity = Mathf.Clamp(percent, 0f, 100f);
            RecalculateFactors();
        }

        public void SetWindSpeed(float speed)
        {
            WindSpeed = Mathf.Clamp(speed, 0f, 100f);
            RecalculateFactors();
        }

        public void SetWaterLevel(float meters)
        {
            WaterLevel = Mathf.Clamp(meters, 0f, 20f);
        }

        public void SetRainfall(float mmPerHour)
        {
            Rainfall = Mathf.Clamp(mmPerHour, 0f, 200f);
        }

        private void RecalculateFactors()
        {
            // Thermal expansion stress: peaks at extremes from 20°C reference
            float tempDelta = Mathf.Abs(Temperature - 20f);
            ThermalStressFactor = tempDelta / 40f; // 0 at 20°C, ~1 at ±40°C

            // Humidity corrosion: higher humidity degrades metal/wood faster
            CorrosionRate = Humidity / 100f * 0.05f;

            // Simplified wind force: F ≈ 0.5 * ρ * v² * A (ρ≈1.2, A≈1m² scale factor)
            WindForceN = 0.6f * WindSpeed * WindSpeed * 0.01f;
        }

        /// <summary>Environmental durability modifier applied to damage accumulation.</summary>
        public float GetDurabilityModifier(MaterialProperties mat)
        {
            if (mat == null) return 1f;
            float humidityPenalty = (1f - mat.WaterResistance) * CorrosionRate;
            return Mathf.Clamp(1f - humidityPenalty, 0.5f, 1f);
        }

        /// <summary>Extra thermal stress in MPa from temperature extremes.</summary>
        public float GetThermalStressMPa(MaterialProperties mat)
        {
            if (mat == null) return 0f;
            // σ_thermal ≈ E * α * ΔT (simplified, E in GPa scaled down)
            float alpha = mat.ThermalExpansionCoefficient * 1e-6f;
            float deltaT = Temperature - 20f;
            return mat.Elasticity * alpha * Mathf.Abs(deltaT) * ThermalStressFactor * 0.01f;
        }

        public void ResetEnvironment()
        {
            Temperature = 20f;
            Humidity = 50f;
            WindSpeed = 0f;
            WaterLevel = 0f;
            Rainfall = 0f;
            RecalculateFactors();
        }
    }
}

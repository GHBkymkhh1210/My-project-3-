using System.Collections;
using UnityEngine;
using DisasterSim.Building;
using DisasterSim.Core;
using DisasterSim.Damage;
using DisasterSim.Environment;

namespace DisasterSim.Disasters
{
    /// <summary>
    /// Orchestrates earthquake, tsunami and windstorm disasters. Each disaster applies
    /// procedural forces and feeds external stress into DamageManager.
    /// </summary>
    public class DisasterManager : MonoBehaviour
    {
        public static DisasterManager Instance { get; private set; }

        [Header("Earthquake")]
        [SerializeField] private float earthquakeDuration = 10f;
        [SerializeField] private float warningCountdown = 3f;
        [SerializeField] private Transform groundTransform;

        [Header("Tsunami")]
        [SerializeField] private float tsunamiDuration = 12f;

        public bool IsDisasterActive { get; private set; }
        public string ActiveDisasterName { get; private set; } = "NONE";
        public float DisasterProgress { get; private set; }

        private Coroutine activeRoutine;
        private Vector3 groundBasePosition;
        private float eqMagnitude;
        private float eqFrequency;
        private float tsunamiWaveHeight;

        private void Awake()
        {
            Instance = this;
            if (groundTransform != null) groundBasePosition = groundTransform.position;
            else
            {
                var ground = GameObject.Find("Ground");
                if (ground != null) { groundTransform = ground.transform; groundBasePosition = groundTransform.position; }
            }
        }

        public void TriggerEarthquake(float magnitude)
        {
            if (IsDisasterActive) { Debug.LogWarning($"[DisasterManager] Ignored EARTHQUAKE - {ActiveDisasterName} is still active. Use Stop Disaster first."); return; }
            eqMagnitude = Mathf.Clamp(magnitude, 1f, 10f);
            eqFrequency = 0.5f + eqMagnitude * 0.3f;
            activeRoutine = StartCoroutine(EarthquakeRoutine());
        }

        public void TriggerTsunami(float waveHeight)
        {
            if (IsDisasterActive) { Debug.LogWarning($"[DisasterManager] Ignored TSUNAMI - {ActiveDisasterName} is still active. Use Stop Disaster first."); return; }
            tsunamiWaveHeight = Mathf.Clamp(waveHeight, 1f, 20f);
            activeRoutine = StartCoroutine(TsunamiRoutine());
        }

        public void TriggerWindstorm(float windSpeed)
        {
            if (IsDisasterActive) { Debug.LogWarning($"[DisasterManager] Ignored WINDSTORM - {ActiveDisasterName} is still active. Use Stop Disaster first."); return; }
            activeRoutine = StartCoroutine(WindstormRoutine(windSpeed));
        }

        public void StopDisaster()
        {
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = null;
            EndDisaster();
        }

        private IEnumerator EarthquakeRoutine()
        {
            IsDisasterActive = true;
            ActiveDisasterName = "EARTHQUAKE";
            SimulationEvents.RaiseDisasterStarted("Earthquake");
            SimulationManager.Instance?.SetDisasterActive(true);

            // Warning countdown
            for (float t = warningCountdown; t > 0f; t -= Time.deltaTime)
                yield return null;

            float elapsed = 0f;
            while (elapsed < earthquakeDuration)
            {
                elapsed += Time.deltaTime;
                DisasterProgress = elapsed / earthquakeDuration;
                float intensity = eqMagnitude / 10f;

                // Procedural ground shake using Perlin noise
                float t = elapsed * eqFrequency;
                float shakeX = (Mathf.PerlinNoise(t, 0f) * 2f - 1f) * intensity * 0.15f;
                float shakeY = (Mathf.PerlinNoise(0f, t) * 2f - 1f) * intensity * 0.05f;
                float shakeZ = (Mathf.PerlinNoise(t, t) * 2f - 1f) * intensity * 0.15f;

                if (groundTransform != null)
                    groundTransform.position = groundBasePosition + new Vector3(shakeX, shakeY, shakeZ);

                ApplyEarthquakeStress(intensity, t);
                yield return null;
            }

            if (groundTransform != null) groundTransform.position = groundBasePosition;
            EndDisaster();
        }

        private void ApplyEarthquakeStress(float intensity, float time)
        {
            var bm = BuildingManager.Instance;
            var dm = DamageManager.Instance;
            var env = EnvironmentManager.Instance;
            if (bm == null || dm == null) return;

            foreach (var comp in bm.AllComponents)
            {
                if (comp == null || comp.HasFailed) continue;

                float heightFactor = 1f + comp.StoryIndex * 0.3f + comp.transform.localScale.y * 0.1f;
                float massFactor = Mathf.Sqrt(comp.Mass);
                float materialResist = comp.Material != null ? comp.Material.EarthquakeResistance : 0.5f;

                // Horizontal acceleration stress: σ ≈ m * a / A, scaled to MPa
                float accel = intensity * 9.81f * heightFactor;
                float noise = Mathf.PerlinNoise(time + comp.GridCell.GetHashCode() * 0.001f, comp.GridCell.x) * 0.5f + 0.5f;
                float stressMPa = (accel * massFactor * noise) / (GetArea(comp) * 1e6f) * 20000f;
                stressMPa *= (1f - materialResist * 0.7f);

                if (env != null) stressMPa += env.GetThermalStressMPa(comp.Material);

                dm.AddExternalStress(comp, stressMPa);
            }
        }

        private IEnumerator TsunamiRoutine()
        {
            IsDisasterActive = true;
            ActiveDisasterName = "TSUNAMI";
            SimulationEvents.RaiseDisasterStarted("Tsunami");
            SimulationManager.Instance?.SetDisasterActive(true);

            var water = WaterSurface.Instance;
            if (water != null) water.StartTsunami(tsunamiWaveHeight, tsunamiDuration, 8f);

            float elapsed = 0f;
            while (elapsed < tsunamiDuration)
            {
                elapsed += Time.deltaTime;
                DisasterProgress = elapsed / tsunamiDuration;
                water?.UpdateTsunami(Time.deltaTime);
                ApplyTsunamiStress();
                yield return null;
            }

            water?.StopTsunami();
            EndDisaster();
        }

        private void ApplyTsunamiStress()
        {
            var bm = BuildingManager.Instance;
            var dm = DamageManager.Instance;
            var water = WaterSurface.Instance;
            if (bm == null || dm == null || water == null) return;

            foreach (var comp in bm.AllComponents)
            {
                if (comp == null || comp.HasFailed) continue;
                if (!water.IsUnderWater(comp.transform.position)) continue;

                float depth = water.CurrentWaterHeight - comp.transform.position.y;
                float materialResist = comp.Material != null ? comp.Material.TsunamiResistance : 0.5f;
                float waterResistance = comp.Material != null ? comp.Material.WaterResistance : 0.5f;

                // Hydrostatic + drag pressure: P = ρgh + drag term (Pa)
                float pressure = depth * 1000f * 9.81f + 2000f * water.TsunamiProgress;
                // Force = pressure * exposed frontal area (matches the wind-load approach below),
                // then Stress = Force / load-bearing cross-section. A load-concentration factor
                // (x15) keeps this within the same visible-damage range as the earthquake model -
                // this is an educational approximation (spec section 28), not a certified FEA result.
                float exposedArea = Mathf.Max(comp.transform.localScale.x * comp.transform.localScale.y, 0.05f);
                float forceN = pressure * exposedArea;
                float stressMPa = (forceN / (GetArea(comp) * 1e6f)) * 15f;
                stressMPa *= (1f - materialResist * 0.6f) * (1f - waterResistance * 0.3f);

                dm.AddExternalStress(comp, stressMPa);
            }
        }

        private IEnumerator WindstormRoutine(float windSpeed)
        {
            IsDisasterActive = true;
            ActiveDisasterName = "WINDSTORM";
            SimulationEvents.RaiseDisasterStarted("Windstorm");
            SimulationManager.Instance?.SetDisasterActive(true);

            var env = EnvironmentManager.Instance;
            float originalWind = env != null ? env.WindSpeed : 0f;
            if (env != null) env.SetWindSpeed(windSpeed);

            float duration = 8f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                DisasterProgress = elapsed / duration;
                ApplyWindStress(windSpeed);
                yield return null;
            }

            if (env != null) env.SetWindSpeed(originalWind);
            EndDisaster();
        }

        private void ApplyWindStress(float windSpeed)
        {
            var bm = BuildingManager.Instance;
            var dm = DamageManager.Instance;
            if (bm == null || dm == null) return;

            float force = 0.6f * windSpeed * windSpeed * 0.01f;
            foreach (var comp in bm.AllComponents)
            {
                if (comp == null || comp.HasFailed) continue;
                float exposedArea = comp.transform.localScale.x * comp.transform.localScale.y;
                float stressMPa = (force * exposedArea / (GetArea(comp) * 1e6f)) * 20f;
                stressMPa *= comp.StructuralImportance;
                dm.AddExternalStress(comp, stressMPa);
            }
        }

        private void EndDisaster()
        {
            IsDisasterActive = false;
            ActiveDisasterName = "NONE";
            DisasterProgress = 0f;
            activeRoutine = null;
            SimulationEvents.RaiseDisasterStopped();
            SimulationManager.Instance?.SetDisasterActive(false);
        }

        private float GetArea(BuildingComponent comp)
        {
            Vector3 s = comp.transform.localScale;
            return Mathf.Max(s.x * s.z, 0.01f);
        }
    }
}

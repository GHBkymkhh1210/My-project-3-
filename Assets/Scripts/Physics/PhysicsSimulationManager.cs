using System.Collections.Generic;
using UnityEngine;
using DisasterSim.Building;
using DisasterSim.Environment;

namespace DisasterSim.Physics
{
    /// <summary>
    /// Simplified structural physics layer. Computes displacement and strain from
    /// applied forces, and feeds results back to BuildingComponent for damage analysis.
    /// Uses Unity Rigidbody only for collapsed pieces; standing structure uses analytical model.
    /// </summary>
    public class PhysicsSimulationManager : MonoBehaviour
    {
        public static PhysicsSimulationManager Instance { get; private set; }

        [SerializeField] private float displacementDamping = 0.92f;

        private readonly Dictionary<BuildingComponent, Vector3> velocities = new();
        private readonly Dictionary<BuildingComponent, Vector3> basePositions = new();

        private void Awake()
        {
            Instance = this;
        }

        private void LateUpdate()
        {
            var bm = BuildingManager.Instance;
            var env = EnvironmentManager.Instance;
            if (bm == null) return;

            float maxDisp = 0f;

            foreach (var comp in bm.AllComponents)
            {
                if (comp == null || comp.HasFailed) continue;

                if (!basePositions.ContainsKey(comp))
                    basePositions[comp] = comp.transform.position;

                Vector3 basePos = basePositions[comp];
                float mass = Mathf.Max(comp.Mass, 0.1f);

                // Simplified: displacement proportional to stress / stiffness
                float stiffness = comp.Elasticity * 1000f; // GPa -> scaled stiffness
                float stressForce = comp.CurrentStressMPa * 1e6f * GetCrossSection(comp);
                float windForce = env != null ? env.WindForceN * comp.StructuralImportance : 0f;
                float totalForce = stressForce + windForce;

                // a = F/m, displacement ≈ F / (k * m) per frame
                float targetDisp = totalForce / (stiffness * mass + 1f) * 0.001f;
                targetDisp = Mathf.Min(targetDisp, 2f);

                if (!velocities.ContainsKey(comp)) velocities[comp] = Vector3.zero;
                Vector3 vel = velocities[comp];
                vel.y += targetDisp * Time.deltaTime * 10f;
                vel *= displacementDamping;
                velocities[comp] = vel;

                comp.CurrentDisplacement = vel.magnitude;
                comp.CurrentStrain = comp.CurrentDisplacement / Mathf.Max(comp.transform.localScale.y, 0.1f);
                maxDisp = Mathf.Max(maxDisp, comp.CurrentDisplacement);

                // Visual sway during disasters (subtle)
                if (comp.CurrentDisplacement > 0.001f)
                {
                    Vector3 offset = new Vector3(
                        Mathf.Sin(Time.time * 8f + comp.GridCell.GetHashCode()) * comp.CurrentDisplacement * 0.3f,
                        0f,
                        Mathf.Cos(Time.time * 6f + comp.GridCell.GetHashCode()) * comp.CurrentDisplacement * 0.3f);
                    comp.transform.position = basePos + offset;
                }
                else
                {
                    comp.transform.position = basePos;
                }
            }

            if (Damage.DamageManager.Instance != null)
                Damage.DamageManager.Instance.SetMaxDisplacement(maxDisp);
        }

        public void RegisterComponent(BuildingComponent comp)
        {
            if (comp != null) basePositions[comp] = comp.transform.position;
        }

        public void ClearTracking()
        {
            velocities.Clear();
            basePositions.Clear();
        }

        private float GetCrossSection(BuildingComponent comp)
        {
            Vector3 s = comp.transform.localScale;
            return comp.ComponentType switch
            {
                BuildingComponentType.Column => s.x * s.z,
                BuildingComponentType.Wall => s.x * s.y,
                _ => s.x * s.z
            };
        }
    }
}

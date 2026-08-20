using System.Collections.Generic;
using UnityEngine;
using DisasterSim.Building;
using DisasterSim.Construction;
using DisasterSim.Core;
using DisasterSim.Environment;

namespace DisasterSim.Damage
{
    /// <summary>
    /// Simplified real-time structural analysis layer (see project spec section 7-8).
    /// Every frame it computes each BuildingComponent's stress (self-weight load-bearing
    /// stress + any external stress contributed this frame by disaster systems), and
    /// accumulates permanent DamagePercent whenever stress exceeds the material's
    /// fracture threshold. This is an educational approximation, not a certified FEA model.
    ///
    /// Disaster systems (Phase 5+) call AddExternalStress(component, stressMPa) during
    /// their own Update(); this manager reads that in LateUpdate so a frame's disaster
    /// forces are always accounted for before damage is resolved.
    /// </summary>
    public class DamageManager : MonoBehaviour
    {
        public static DamageManager Instance { get; private set; }

        [Header("Physics")]
        [SerializeField] private float gravity = 9.81f;

        [Header("Damage Accumulation")]
        [Tooltip("How quickly damage accumulates (per second) per unit of stress-ratio overage above the fracture threshold.")]
        [SerializeField] private float damageRate = 0.15f;

        [Header("Damage Thresholds (fraction of DamagePercent)")]
        [SerializeField] private float minorThreshold = 0.25f;
        [SerializeField] private float moderateThreshold = 0.50f;
        [SerializeField] private float severeThreshold = 0.75f;

        private readonly Dictionary<BuildingComponent, float> externalStress = new();

        public float OverallDamagePercent { get; private set; }
        public float StructuralIntegrityPercent { get; private set; } = 100f;
        public int FailedComponentCount { get; private set; }
        public int TotalComponentCount { get; private set; }
        public float MaxStressMPa { get; private set; }
        public float MaxDisplacement { get; private set; }
        public float MaxStrain { get; private set; }
        public string StabilityLabel { get; private set; } = "STABLE";

        public void SetMaxDisplacement(float d) => MaxDisplacement = d;

        private void Awake()
        {
            Instance = this;
        }

        private void LateUpdate()
        {
            var bm = BuildingManager.Instance;
            var grid = ConstructionGridManager.Instance;
            if (bm == null || grid == null) return;

            float cellSize = grid.CellSize;
            float totalDamage = 0f;
            int failed = 0;
            int total = 0;
            float maxStress = 0f;

            float maxStrain = 0f;

            // Snapshot so we can safely untrack components that fail during iteration.
            var components = new List<BuildingComponent>(bm.AllComponents);

            foreach (var comp in components)
            {
                if (comp == null) continue;
                total++;

                float gravityStress = ComputeGravityStressMPa(comp, bm, cellSize);
                externalStress.TryGetValue(comp, out float extra);
                float totalStress = gravityStress + extra;
                comp.CurrentStressMPa = totalStress;
                maxStress = Mathf.Max(maxStress, totalStress);
                maxStrain = Mathf.Max(maxStrain, comp.CurrentStrain);

                ApplyDamage(comp, totalStress);

                totalDamage += comp.DamagePercent;
                if (comp.Status == StructuralStatus.Failed) failed++;
            }

            TotalComponentCount = total;
            FailedComponentCount = failed;
            OverallDamagePercent = total > 0 ? (totalDamage / total) * 100f : 0f;
            StructuralIntegrityPercent = 100f - OverallDamagePercent;
            MaxStressMPa = maxStress;
            MaxStrain = maxStrain;
            StabilityLabel = failed > total * 0.5f ? "COLLAPSING" : failed > 0 ? "UNSTABLE" : "STABLE";

            SimulationEvents.RaiseDamageStatsUpdated();

            externalStress.Clear();
        }

        /// <summary>Disaster systems call this each frame (during their own Update) to
        /// contribute additional stress to a component before damage is resolved.</summary>
        public void AddExternalStress(BuildingComponent comp, float stressMPa)
        {
            if (comp == null || stressMPa <= 0f) return;
            externalStress.TryGetValue(comp, out float current);
            externalStress[comp] = current + stressMPa;
        }

        /// <summary>
        /// Static load-bearing stress from self-weight: Foundation/Column/Wall pieces
        /// carry the mass of whatever Beam/Floor/Roof sits above them in the same cell
        /// (Foundation additionally carries the Column/Wall mass too). Stress = Force / Area.
        /// </summary>
        private float ComputeGravityStressMPa(BuildingComponent comp, BuildingManager bm, float cellSize)
        {
            float loadMass = comp.Mass; // self-weight always counts

            if (comp.ComponentType is BuildingComponentType.Foundation or BuildingComponentType.Column or BuildingComponentType.Wall)
            {
                BuildingComponentType[] loadTypes = { BuildingComponentType.Beam, BuildingComponentType.Floor, BuildingComponentType.Roof };
                foreach (var t in loadTypes)
                {
                    if (bm.Placed.TryGetValue((comp.GridCell, t, comp.StoryIndex), out var loadComp))
                        loadMass += loadComp.Mass;
                }
            }

            if (comp.ComponentType == BuildingComponentType.Foundation)
            {
                BuildingComponentType[] supportedTypes = { BuildingComponentType.Column, BuildingComponentType.Wall };
                foreach (var t in supportedTypes)
                {
                    if (bm.Placed.TryGetValue((comp.GridCell, t, comp.StoryIndex), out var supComp))
                        loadMass += supComp.Mass;
                }
            }

            float forceN = loadMass * gravity;
            float areaM2 = Mathf.Max(bm.GetCrossSectionArea(comp.ComponentType, cellSize), 0.0001f);
            float stressPa = forceN / areaM2;
            return stressPa / 1_000_000f; // Pa -> MPa
        }

        private void ApplyDamage(BuildingComponent comp, float totalStressMPa)
        {
            float strength = Mathf.Max(comp.CompressiveStrength, 0.001f);
            float fractureThreshold = comp.Material != null ? comp.Material.FractureThreshold : 0.6f;
            float ratio = totalStressMPa / strength;

            var env = EnvironmentManager.Instance;
            float envMod = env != null && comp.Material != null ? env.GetDurabilityModifier(comp.Material) : 1f;
            float thermalStress = env != null ? env.GetThermalStressMPa(comp.Material) : 0f;
            ratio += thermalStress / strength;

            if (ratio > fractureThreshold)
            {
                float overage = ratio - fractureThreshold;
                comp.DamagePercent = Mathf.Clamp01(comp.DamagePercent + overage * damageRate * Time.deltaTime / envMod);
            }

            var previousStatus = comp.Status;
            comp.Status = comp.DamagePercent switch
            {
                >= 1f => StructuralStatus.Failed,
                var d when d >= severeThreshold => StructuralStatus.Severe,
                var d when d >= moderateThreshold => StructuralStatus.Moderate,
                var d when d >= minorThreshold => StructuralStatus.Minor,
                _ => StructuralStatus.Healthy
            };

            if (previousStatus != comp.Status)
            {
                comp.RefreshDamageColor();
                if (comp.Status == StructuralStatus.Failed && !comp.HasFailed)
                {
                    CollapseComponent(comp);
                }
            }
        }

        /// <summary>A piece that has taken critical damage detaches from the structure and
        /// falls under normal physics, per spec section 8 ("Potentially detach/collapse").</summary>
        private void CollapseComponent(BuildingComponent comp)
        {
            comp.HasFailed = true;
            BuildingManager.Instance.ForceUntrack(comp);

            if (comp.GetComponent<Rigidbody>() == null)
            {
                var rb = comp.gameObject.AddComponent<Rigidbody>();
                rb.mass = Mathf.Max(comp.Mass, 1f);
            }

            SpawnFailureBurst(comp.transform.position);
        }

        /// <summary>Small procedural dust/debris burst played once when a component fails
        /// (spec section 14: "Add particle effects for severe damage"). Built entirely from
        /// code so no external particle asset is required; self-destructs after playing.</summary>
        private void SpawnFailureBurst(Vector3 position)
        {
            var go = new GameObject("FailureBurst");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 3f;
            main.startSize = 0.25f;
            main.startColor = new Color(0.4f, 0.35f, 0.3f, 0.9f);
            main.gravityModifier = 0.6f;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

            Destroy(go, main.duration + main.startLifetime.constantMax + 0.2f);
        }
    }
}

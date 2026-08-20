using System;
using UnityEngine;
using DisasterSim.Core;
using DisasterSim.Damage;

namespace DisasterSim.Utilities
{
    [Serializable]
    public struct ExperimentSnapshot
    {
        public string Label;
        public float OverallDamage;
        public float StructuralIntegrity;
        public int FailedComponents;
        public int TotalComponents;
        public float MaxDisplacement;
        public float MaxStress;
        public string Stability;
        public float TotalMass;
    }

    /// <summary>
    /// Records before/after disaster snapshots so users can compare architectural designs.
    /// </summary>
    public class ExperimentManager : MonoBehaviour
    {
        public static ExperimentManager Instance { get; private set; }

        public ExperimentSnapshot DesignA { get; private set; }
        public ExperimentSnapshot DesignB { get; private set; }
        public bool HasDesignA { get; private set; }
        public bool HasDesignB { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public void RecordDesignA()
        {
            DesignA = CaptureSnapshot("Design A");
            HasDesignA = true;
            SimulationEvents.RaiseExperimentSnapshotTaken();
        }

        public void RecordDesignB()
        {
            DesignB = CaptureSnapshot("Design B");
            HasDesignB = true;
            SimulationEvents.RaiseExperimentSnapshotTaken();
        }

        public float GetDamageReduction()
        {
            if (!HasDesignA || !HasDesignB || DesignA.OverallDamage <= 0f) return 0f;
            return 1f - (DesignB.OverallDamage / DesignA.OverallDamage);
        }

        private ExperimentSnapshot CaptureSnapshot(string label)
        {
            var dm = DamageManager.Instance;
            var bm = Building.BuildingManager.Instance;
            float totalMass = 0f;
            if (bm != null)
                foreach (var c in bm.AllComponents)
                    if (c != null) totalMass += c.Mass;

            return new ExperimentSnapshot
            {
                Label = label,
                OverallDamage = dm != null ? dm.OverallDamagePercent : 0f,
                StructuralIntegrity = dm != null ? dm.StructuralIntegrityPercent : 100f,
                FailedComponents = dm != null ? dm.FailedComponentCount : 0,
                TotalComponents = dm != null ? dm.TotalComponentCount : 0,
                MaxDisplacement = dm != null ? dm.MaxDisplacement : 0f,
                MaxStress = dm != null ? dm.MaxStressMPa : 0f,
                Stability = dm != null ? dm.StabilityLabel : "STABLE",
                TotalMass = totalMass
            };
        }
    }
}

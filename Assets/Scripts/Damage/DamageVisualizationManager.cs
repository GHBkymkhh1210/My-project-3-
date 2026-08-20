using UnityEngine;
using DisasterSim.Building;

namespace DisasterSim.Damage
{
    public enum VisualizationMode
    {
        Normal,
        Stress,
        Displacement,
        Damage,
        Force
    }

    /// <summary>
    /// Centralized damage/stress visualization. Switches component colors based on
    /// the active visualization mode without hard-coding colors in each component.
    /// </summary>
    public class DamageVisualizationManager : MonoBehaviour
    {
        public static DamageVisualizationManager Instance { get; private set; }

        public VisualizationMode CurrentMode { get; private set; } = VisualizationMode.Normal;

        private void Awake()
        {
            Instance = this;
            DisasterSim.Core.SimulationEvents.OnDamageStatsUpdated += HandleDamageStatsUpdated;
        }

        private void OnDestroy()
        {
            DisasterSim.Core.SimulationEvents.OnDamageStatsUpdated -= HandleDamageStatsUpdated;
        }

        private void HandleDamageStatsUpdated()
        {
            // Keep the heatmap live while a non-Normal mode is active, since stress/
            // displacement/damage change every frame during an active disaster.
            if (CurrentMode != VisualizationMode.Normal) RefreshAll();
        }

        public void SetMode(VisualizationMode mode)
        {
            CurrentMode = mode;
            RefreshAll();
        }

        public void RefreshAll()
        {
            var bm = BuildingManager.Instance;
            if (bm == null) return;
            foreach (var comp in bm.AllComponents)
                if (comp != null) ApplyVisualization(comp);
        }

        public void ApplyVisualization(BuildingComponent comp)
        {
            if (comp == null) return;

            if (CurrentMode == VisualizationMode.Normal)
            {
                comp.RefreshDamageColor();
                return;
            }

            Color color = CurrentMode switch
            {
                VisualizationMode.Stress => StressColor(comp),
                VisualizationMode.Displacement => DisplacementColor(comp),
                VisualizationMode.Damage => DamageColor(comp),
                VisualizationMode.Force => ForceColor(comp),
                _ => Color.white
            };

            SetComponentColor(comp, color);
        }

        private Color StressColor(BuildingComponent comp)
        {
            float ratio = comp.CurrentStressMPa / Mathf.Max(comp.CompressiveStrength, 0.001f);
            return Color.Lerp(Color.green, Color.red, Mathf.Clamp01(ratio));
        }

        private Color DisplacementColor(BuildingComponent comp)
        {
            return Color.Lerp(Color.blue, Color.magenta, Mathf.Clamp01(comp.CurrentDisplacement / 2f));
        }

        private Color DamageColor(BuildingComponent comp)
        {
            return comp.Status switch
            {
                StructuralStatus.Minor => Color.yellow,
                StructuralStatus.Moderate => new Color(1f, 0.5f, 0f),
                StructuralStatus.Severe => Color.red,
                StructuralStatus.Failed => new Color(0.2f, 0f, 0f),
                _ => Color.green
            };
        }

        private Color ForceColor(BuildingComponent comp)
        {
            float f = comp.CurrentStressMPa * comp.StructuralImportance;
            return Color.Lerp(Color.cyan, Color.red, Mathf.Clamp01(f / 100f));
        }

        private void SetComponentColor(BuildingComponent comp, Color color)
        {
            var renderers = comp.GetComponentsInChildren<Renderer>();
            var block = new MaterialPropertyBlock();
            int id = Shader.PropertyToID("_BaseColor");
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor(id, color);
                r.SetPropertyBlock(block);
            }
        }
    }
}

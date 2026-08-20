using UnityEngine;

namespace DisasterSim.Building
{
    public enum StructuralStatus
    {
        Healthy,
        Minor,
        Moderate,
        Severe,
        Failed
    }

    /// <summary>
    /// Data + identity for a single placed structural piece (foundation, column, beam,
    /// wall, floor or roof). The Physics/Damage system (Phase 4+) reads and writes the
    /// runtime fields here rather than each having their own bookkeeping.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildingComponent : MonoBehaviour
    {
        [Header("Identity")]
        public BuildingComponentType ComponentType;
        public Vector2Int GridCell;
        public int StoryIndex;

        [Header("Material (assigned from the selected MaterialProperties at placement time)")]
        public MaterialProperties Material;
        public float Mass = 1f;
        public float TensileStrength = 100f;
        public float CompressiveStrength = 100f;
        public float Elasticity = 1f;
        public float MaxStress = 100f;

        [Header("Structural Importance (0-1, load-bearing weight)")]
        [Range(0f, 1f)] public float StructuralImportance = 0.5f;

        [Header("Runtime Structural State (written by DamageManager every frame)")]
        [Tooltip("Current combined stress in MPa (self-weight + any disaster forces).")]
        public float CurrentStressMPa;
        [Tooltip("Normalized strain (displacement / original dimension).")]
        public float CurrentStrain;
        [Tooltip("Current displacement in meters.")]
        public float CurrentDisplacement;
        [Range(0f, 1f)] public float DamagePercent;
        public StructuralStatus Status = StructuralStatus.Healthy;
        public bool HasFailed;
        [Tooltip("Configurable failure threshold (fraction of max stress).")]
        public float FailureThreshold = 0.75f;

        private Renderer[] cachedRenderers;
        private MaterialPropertyBlock propertyBlock;
        private Color baseColor = Color.white;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>Applies the chosen construction material's data and tint to this piece.</summary>
        public void ApplyMaterial(MaterialProperties material, float volumeCubicMeters)
        {
            Material = material;
            TensileStrength = material.TensileStrength;
            CompressiveStrength = material.CompressiveStrength;
            Elasticity = material.Elasticity;
            MaxStress = material.CompressiveStrength;
            Mass = material.Density * volumeCubicMeters;

            baseColor = material.DisplayColor;
            RefreshDamageColor();
        }

        /// <summary>
        /// Tints the component based on its current damage Status, blending from the
        /// material's own color (Healthy) towards a damage-indicator color as Status
        /// worsens. Called by DamageManager whenever Status/DamagePercent changes.
        /// </summary>
        public void RefreshDamageColor()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0) return;

            Color damageColor = Status switch
            {
                StructuralStatus.Minor => new Color(0.9f, 0.9f, 0.2f),
                StructuralStatus.Moderate => new Color(0.95f, 0.6f, 0.1f),
                StructuralStatus.Severe => new Color(0.85f, 0.15f, 0.1f),
                StructuralStatus.Failed => new Color(0.25f, 0.05f, 0.05f),
                _ => baseColor
            };

            float blend = Status == StructuralStatus.Healthy ? 0f : Mathf.Clamp01(0.5f + DamagePercent * 0.5f);
            Color final = Color.Lerp(baseColor, damageColor, blend);

            foreach (var r in cachedRenderers)
            {
                r.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, final);
                r.SetPropertyBlock(propertyBlock);
            }
        }
    }
}

using UnityEngine;

namespace DisasterSim.Building
{
    /// <summary>
    /// Data-only description of a construction material. Values are simplified,
    /// educational approximations (not certified engineering data) intended to
    /// produce clear, consistent cause-and-effect behaviour in the simulation
    /// rather than professionally accurate structural analysis.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMaterial", menuName = "Disaster Sim/Material Properties")]
    public class MaterialProperties : ScriptableObject
    {
        [Header("Identity")]
        public string MaterialDisplayName = "Material";
        public Color DisplayColor = Color.gray;

        [Header("Physical")]
        [Tooltip("kg/m^3")] public float Density = 2000f;
        [Tooltip("MPa")] public float TensileStrength = 10f;
        [Tooltip("MPa")] public float CompressiveStrength = 20f;
        [Tooltip("GPa, higher = stiffer/less flexible")] public float Elasticity = 20f;

        [Header("Damage Behaviour")]
        [Range(0f, 1f)] public float FractureThreshold = 0.5f;
        [Range(0f, 1f)] public float Durability = 0.5f;
        [Tooltip("x10^-6 per degree C")] public float ThermalExpansionCoefficient = 10f;

        [Header("Environmental / Disaster Resistance (0-1, higher is better)")]
        [Range(0f, 1f)] public float WaterResistance = 0.5f;
        [Range(0f, 1f)] public float EarthquakeResistance = 0.5f;
        [Range(0f, 1f)] public float TsunamiResistance = 0.5f;
    }
}

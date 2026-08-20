using UnityEngine;

namespace DisasterSim.Disasters
{
    /// <summary>Data-driven disaster parameters stored as ScriptableObjects.</summary>
    [CreateAssetMenu(fileName = "NewDisaster", menuName = "Disaster Sim/Disaster Properties")]
    public class DisasterProperties : ScriptableObject
    {
        public string DisasterName = "Disaster";
        [Range(1f, 10f)] public float DefaultMagnitude = 5f;
        public float DefaultDuration = 10f;
        public float DefaultFrequency = 2f;
        public float WarningCountdown = 3f;
    }
}

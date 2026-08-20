using UnityEngine;

namespace DisasterSim.Environment
{
    [CreateAssetMenu(fileName = "NewEnvironment", menuName = "Disaster Sim/Environment Properties")]
    public class EnvironmentProperties : ScriptableObject
    {
        public float DefaultTemperature = 20f;
        public float DefaultHumidity = 50f;
        public float DefaultWindSpeed = 0f;
        public float DefaultWaterLevel = 0f;
        public float DefaultRainfall = 0f;
    }
}

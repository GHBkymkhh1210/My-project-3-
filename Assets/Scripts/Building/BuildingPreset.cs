using System.Collections.Generic;
using UnityEngine;
using DisasterSim.Building;

namespace DisasterSim.Building
{
    [CreateAssetMenu(fileName = "NewPreset", menuName = "Disaster Sim/Building Preset")]
    public class BuildingPreset : ScriptableObject
    {
        public string PresetName = "Preset";
        [TextArea] public string Description;
        public List<BuildingComponentData> Components = new();
    }
}

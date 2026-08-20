using UnityEngine;
using DisasterSim.Building;

namespace DisasterSim.Utilities
{
    /// <summary>Loads predefined building designs from ScriptableObject presets.</summary>
    public class PresetLoader : MonoBehaviour
    {
        [SerializeField] private BuildingPreset[] presets;

        public BuildingPreset[] Presets => presets;

        public void LoadPreset(int index)
        {
            if (presets == null || index < 0 || index >= presets.Length) return;
            LoadPreset(presets[index]);
        }

        public void LoadPreset(BuildingPreset preset)
        {
            if (preset == null || BuildingManager.Instance == null) return;
            BuildingManager.Instance.LoadFromData(preset.Components);
            Debug.Log($"[PresetLoader] Loaded preset: {preset.PresetName}");
        }

        public void LoadPresetByName(string name)
        {
            if (presets == null) return;
            foreach (var p in presets)
                if (p != null && p.PresetName == name) { LoadPreset(p); return; }
        }
    }
}

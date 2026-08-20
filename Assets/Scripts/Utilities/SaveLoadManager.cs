using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DisasterSim.Building;
using DisasterSim.Environment;

namespace DisasterSim.Utilities
{
    [Serializable]
    public class BuildingSaveFile
    {
        public string DesignName;
        public List<BuildingComponentData> Components = new();
        public float Temperature = 20f;
        public float Humidity = 50f;
        public float WindSpeed;
        public float WaterLevel;
    }

    /// <summary>JSON save/load for building designs and environment settings.</summary>
    public class SaveLoadManager : MonoBehaviour
    {
        public static SaveLoadManager Instance { get; private set; }

        private string SaveDirectory => Path.Combine(Application.persistentDataPath, "BuildingDesigns");

        private void Awake()
        {
            Instance = this;
            Directory.CreateDirectory(SaveDirectory);
        }

        public void SaveDesign(string name)
        {
            var bm = BuildingManager.Instance;
            var env = EnvironmentManager.Instance;
            if (bm == null) return;

            var file = new BuildingSaveFile
            {
                DesignName = name,
                Components = bm.ExportData(),
                Temperature = env != null ? env.Temperature : 20f,
                Humidity = env != null ? env.Humidity : 50f,
                WindSpeed = env != null ? env.WindSpeed : 0f,
                WaterLevel = env != null ? env.WaterLevel : 0f
            };

            string path = Path.Combine(SaveDirectory, Sanitize(name) + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(file, true));
            Debug.Log($"[SaveLoad] Saved design '{name}' to {path}");
        }

        public bool LoadDesign(string name)
        {
            string path = Path.Combine(SaveDirectory, Sanitize(name) + ".json");
            if (!File.Exists(path)) return false;

            var file = JsonUtility.FromJson<BuildingSaveFile>(File.ReadAllText(path));
            BuildingManager.Instance?.LoadFromData(file.Components);

            var env = EnvironmentManager.Instance;
            if (env != null)
            {
                env.SetTemperature(file.Temperature);
                env.SetHumidity(file.Humidity);
                env.SetWindSpeed(file.WindSpeed);
                env.SetWaterLevel(file.WaterLevel);
            }
            Debug.Log($"[SaveLoad] Loaded design '{name}'");
            return true;
        }

        public void DeleteDesign(string name)
        {
            string path = Path.Combine(SaveDirectory, Sanitize(name) + ".json");
            if (File.Exists(path)) File.Delete(path);
        }

        public List<string> ListDesigns()
        {
            var list = new List<string>();
            if (!Directory.Exists(SaveDirectory)) return list;
            foreach (var f in Directory.GetFiles(SaveDirectory, "*.json"))
                list.Add(Path.GetFileNameWithoutExtension(f));
            return list;
        }

        private static string Sanitize(string name) =>
            string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
    }
}

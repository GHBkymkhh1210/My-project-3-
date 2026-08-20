using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DisasterSim.Construction;
using DisasterSim.Core;
using DisasterSim.UI;

namespace DisasterSim.Building
{
    /// <summary>
    /// Grid-based building construction: place, delete, move and rotate modular
    /// structural components. Tracks every placed piece for physics/damage systems.
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        [Header("Story Height")]
        [SerializeField] private float storyHeight = 3f;

        [Header("Component Dimensions (meters)")]
        [SerializeField] private Vector3 foundationSize = new(0.95f, 0.5f, 0.95f);
        [SerializeField] private Vector3 columnSize = new(0.4f, 2.8f, 0.4f);
        [SerializeField] private Vector3 beamSize = new(0.95f, 0.3f, 0.4f);
        [SerializeField] private Vector3 wallSize = new(0.95f, 2.8f, 0.15f);
        [SerializeField] private Vector3 floorSize = new(0.95f, 0.25f, 0.95f);
        [SerializeField] private Vector3 roofSize = new(0.95f, 0.3f, 0.95f);

        [Header("Materials")]
        [SerializeField] private MaterialProperties[] materialAssets;
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Material[] componentMaterials;

        public BuildTool CurrentTool { get; private set; } = BuildTool.None;
        public MaterialProperties SelectedMaterial { get; private set; }
        public IReadOnlyList<MaterialProperties> AvailableMaterials => materialAssets;
        public int CurrentStory { get; private set; }
        public BuildingComponent SelectedComponent { get; private set; }

        /// <summary>Key = (grid cell, component type, story index).</summary>
        public Dictionary<(Vector2Int, BuildingComponentType, int), BuildingComponent> Placed { get; private set; } = new();

        private readonly List<BuildingComponent> allComponents = new();
        private GameObject ghostPreview;
        private BuildingComponent moveSource;
        private float placementRotationY;

        private void Awake()
        {
            Instance = this;
            if (materialAssets != null && materialAssets.Length > 0)
                SelectedMaterial = materialAssets[0];

            SimulationEvents.OnBuildingReset += ResetBuilding;
        }

        private void OnDestroy()
        {
            SimulationEvents.OnBuildingReset -= ResetBuilding;
        }

        private void Update()
        {
            if (UIManager.Instance != null && UIManager.Instance.IsPointerOverInteractiveUI()) return;

            HandleStoryScroll();
            UpdateGhostPreview();
            HandleInput();
        }

        public void SetTool(BuildTool tool)
        {
            CurrentTool = tool;
            moveSource = null;
            DestroyGhost();
        }

        public void SetMaterialByName(string name)
        {
            if (materialAssets == null) return;
            foreach (var m in materialAssets)
            {
                if (m != null && m.MaterialDisplayName == name)
                {
                    SelectedMaterial = m;
                    return;
                }
            }
        }

        public IReadOnlyList<BuildingComponent> AllComponents => allComponents;

        public float GetCrossSectionArea(BuildingComponentType type, float cellSize)
        {
            Vector3 size = GetDefaultSize(type);
            return type switch
            {
                BuildingComponentType.Column => size.x * size.z,
                BuildingComponentType.Wall => size.x * size.y,
                BuildingComponentType.Beam => size.x * size.z,
                BuildingComponentType.Foundation => size.x * size.z,
                _ => size.x * size.z
            };
        }

        public Vector3 GetDefaultSize(BuildingComponentType type) => type switch
        {
            BuildingComponentType.Foundation => foundationSize,
            BuildingComponentType.Column => columnSize,
            BuildingComponentType.Beam => beamSize,
            BuildingComponentType.Wall => wallSize,
            BuildingComponentType.Floor => floorSize,
            BuildingComponentType.Roof => roofSize,
            _ => Vector3.one
        };

        public void ForceUntrack(BuildingComponent comp)
        {
            if (comp == null) return;
            Placed.Remove((comp.GridCell, comp.ComponentType, comp.StoryIndex));
            allComponents.Remove(comp);
        }

        private void HandleStoryScroll()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && Keyboard.current != null && Keyboard.current.ctrlKey.isPressed)
            {
                CurrentStory = Mathf.Max(0, CurrentStory + (scroll > 0 ? 1 : -1));
            }
        }

        private void HandleInput()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            var grid = ConstructionGridManager.Instance;
            if (grid == null || !grid.HoveredCell.HasValue) return;

            Vector2Int cell = grid.HoveredCell.Value;
            if (!grid.IsCellInBounds(cell)) return;

            switch (CurrentTool)
            {
                case BuildTool.PlaceFoundation: TryPlace(cell, BuildingComponentType.Foundation, 0); break;
                case BuildTool.PlaceColumn: TryPlace(cell, BuildingComponentType.Column, CurrentStory); break;
                case BuildTool.PlaceBeam: TryPlace(cell, BuildingComponentType.Beam, CurrentStory); break;
                case BuildTool.PlaceWall: TryPlace(cell, BuildingComponentType.Wall, CurrentStory); break;
                case BuildTool.PlaceFloor: TryPlace(cell, BuildingComponentType.Floor, CurrentStory); break;
                case BuildTool.PlaceRoof: TryPlace(cell, BuildingComponentType.Roof, CurrentStory); break;
                case BuildTool.Delete: TryDelete(cell); break;
                case BuildTool.Move: HandleMove(cell); break;
                case BuildTool.Rotate:
                    placementRotationY = (placementRotationY + 90f) % 360f;
                    break;
                case BuildTool.None:
                    SelectComponentAt(cell);
                    break;
            }
        }

        private void TryPlace(Vector2Int cell, BuildingComponentType type, int story)
        {
            var key = (cell, type, story);
            if (Placed.ContainsKey(key)) return;

            if (type == BuildingComponentType.Foundation)
            {
                foreach (var k in Placed.Keys)
                    if (k.Item1 == cell && k.Item3 == 0 && k.Item2 == BuildingComponentType.Foundation) return;
            }

            if (type != BuildingComponentType.Foundation && story > 0)
            {
                bool hasSupport = Placed.ContainsKey((cell, BuildingComponentType.Foundation, 0))
                    || Placed.ContainsKey((cell, BuildingComponentType.Column, story - 1))
                    || Placed.ContainsKey((cell, BuildingComponentType.Wall, story - 1));
                if (!hasSupport && story > 0) return;
            }

            CreateComponent(cell, type, story);
        }

        private void TryDelete(Vector2Int cell)
        {
            BuildingComponent best = null;
            int bestStory = -1;
            foreach (var kv in Placed)
            {
                if (kv.Key.Item1 != cell) continue;
                if (kv.Key.Item3 >= bestStory) { bestStory = kv.Key.Item3; best = kv.Value; }
            }
            if (best != null) RemoveComponent(best);
        }

        private void HandleMove(Vector2Int cell)
        {
            if (moveSource == null)
            {
                foreach (var kv in Placed)
                {
                    if (kv.Key.Item1 == cell) { moveSource = kv.Value; return; }
                }
            }
            else
            {
                var oldKey = (moveSource.GridCell, moveSource.ComponentType, moveSource.StoryIndex);
                Placed.Remove(oldKey);
                moveSource.GridCell = cell;
                RepositionComponent(moveSource);
                Placed[(cell, moveSource.ComponentType, moveSource.StoryIndex)] = moveSource;
                moveSource = null;
            }
        }

        private void CreateComponent(Vector2Int cell, BuildingComponentType type, int story)
        {
            var grid = ConstructionGridManager.Instance;
            Vector3 size = GetDefaultSize(type);
            Vector3 center = grid.CellToWorldCenter(cell);
            float yBase = grid.GridOrigin.y + story * storyHeight;
            float yPos = yBase + size.y * 0.5f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"{type}_{cell.x}_{cell.y}_S{story}";
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(center.x, yPos, center.z);
            go.transform.localScale = size;
            go.transform.rotation = Quaternion.Euler(0f, placementRotationY, 0f);

            var comp = go.AddComponent<BuildingComponent>();
            comp.ComponentType = type;
            comp.GridCell = cell;
            comp.StoryIndex = story;
            comp.StructuralImportance = type switch
            {
                BuildingComponentType.Foundation => 1f,
                BuildingComponentType.Column => 0.9f,
                BuildingComponentType.Wall => 0.7f,
                BuildingComponentType.Beam => 0.8f,
                _ => 0.5f
            };

            float volume = size.x * size.y * size.z;
            if (SelectedMaterial != null) comp.ApplyMaterial(SelectedMaterial, volume);

            int matIndex = (int)type;
            if (componentMaterials != null && matIndex < componentMaterials.Length && componentMaterials[matIndex] != null)
            {
                var r = go.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = componentMaterials[matIndex];
            }

            Placed[(cell, type, story)] = comp;
            allComponents.Add(comp);
        }

        private void RepositionComponent(BuildingComponent comp)
        {
            var grid = ConstructionGridManager.Instance;
            Vector3 size = GetDefaultSize(comp.ComponentType);
            Vector3 center = grid.CellToWorldCenter(comp.GridCell);
            float yBase = grid.GridOrigin.y + comp.StoryIndex * storyHeight;
            comp.transform.position = new Vector3(center.x, yBase + size.y * 0.5f, center.z);
        }

        private void RemoveComponent(BuildingComponent comp)
        {
            Placed.Remove((comp.GridCell, comp.ComponentType, comp.StoryIndex));
            allComponents.Remove(comp);
            if (SelectedComponent == comp) { SelectedComponent = null; SimulationEvents.RaiseComponentDeselected(); }
            Destroy(comp.gameObject);
        }

        public void ResetBuilding()
        {
            foreach (var comp in new List<BuildingComponent>(allComponents))
                if (comp != null) Destroy(comp.gameObject);
            Placed.Clear();
            allComponents.Clear();
            CurrentStory = 0;
            moveSource = null;
            SelectedComponent = null;
            DestroyGhost();
        }

        public void SelectComponentAt(Vector2Int cell)
        {
            BuildingComponent best = null;
            int bestStory = -1;
            foreach (var kv in Placed)
            {
                if (kv.Key.Item1 != cell) continue;
                if (kv.Key.Item3 >= bestStory) { bestStory = kv.Key.Item3; best = kv.Value; }
            }
            SelectedComponent = best;
            if (best != null) SimulationEvents.RaiseComponentSelected(best);
            else SimulationEvents.RaiseComponentDeselected();
        }

        public void LoadFromData(List<BuildingComponentData> data)
        {
            ResetBuilding();
            foreach (var d in data)
            {
                CreateComponent(d.GridCell, d.ComponentType, d.StoryIndex);
                var key = (d.GridCell, d.ComponentType, d.StoryIndex);
                if (Placed.TryGetValue(key, out var comp))
                {
                    comp.transform.localScale = d.Dimensions;
                    comp.transform.rotation = Quaternion.Euler(0f, d.RotationY, 0f);
                    if (!string.IsNullOrEmpty(d.MaterialName))
                    {
                        SetMaterialByName(d.MaterialName);
                        float vol = d.Dimensions.x * d.Dimensions.y * d.Dimensions.z;
                        comp.ApplyMaterial(SelectedMaterial, vol);
                    }
                }
            }
        }

        public List<BuildingComponentData> ExportData()
        {
            var list = new List<BuildingComponentData>();
            foreach (var comp in allComponents)
            {
                if (comp == null) continue;
                list.Add(new BuildingComponentData
                {
                    ComponentType = comp.ComponentType,
                    GridCell = comp.GridCell,
                    StoryIndex = comp.StoryIndex,
                    Dimensions = comp.transform.localScale,
                    RotationY = comp.transform.eulerAngles.y,
                    MaterialName = comp.Material != null ? comp.Material.MaterialDisplayName : ""
                });
            }
            return list;
        }

        private void UpdateGhostPreview()
        {
            if (CurrentTool is BuildTool.None or BuildTool.Delete or BuildTool.Move or BuildTool.Rotate)
            {
                DestroyGhost();
                return;
            }

            var grid = ConstructionGridManager.Instance;
            if (grid == null || !grid.HoveredCell.HasValue) { DestroyGhost(); return; }

            BuildingComponentType? type = CurrentTool switch
            {
                BuildTool.PlaceFoundation => BuildingComponentType.Foundation,
                BuildTool.PlaceColumn => BuildingComponentType.Column,
                BuildTool.PlaceBeam => BuildingComponentType.Beam,
                BuildTool.PlaceWall => BuildingComponentType.Wall,
                BuildTool.PlaceFloor => BuildingComponentType.Floor,
                BuildTool.PlaceRoof => BuildingComponentType.Roof,
                _ => null
            };
            if (!type.HasValue) return;

            int story = type == BuildingComponentType.Foundation ? 0 : CurrentStory;
            Vector3 size = GetDefaultSize(type.Value);
            Vector3 center = grid.CellToWorldCenter(grid.HoveredCell.Value);
            float yBase = grid.GridOrigin.y + story * storyHeight;
            Vector3 pos = new(center.x, yBase + size.y * 0.5f, center.z);

            if (ghostPreview == null)
            {
                ghostPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ghostPreview.name = "GhostPreview";
                Object.Destroy(ghostPreview.GetComponent<Collider>());
                var r = ghostPreview.GetComponent<Renderer>();
                if (ghostMaterial != null) r.sharedMaterial = ghostMaterial;
            }

            ghostPreview.transform.position = pos;
            ghostPreview.transform.localScale = size;
            ghostPreview.transform.rotation = Quaternion.Euler(0f, placementRotationY, 0f);
            ghostPreview.SetActive(true);
        }

        private void DestroyGhost()
        {
            if (ghostPreview != null) ghostPreview.SetActive(false);
        }
    }

    [System.Serializable]
    public struct BuildingComponentData
    {
        public BuildingComponentType ComponentType;
        public Vector2Int GridCell;
        public int StoryIndex;
        public Vector3 Dimensions;
        public float RotationY;
        public string MaterialName;
    }
}

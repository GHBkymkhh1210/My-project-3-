using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using DisasterSim.Building;
using DisasterSim.Core;
using DisasterSim.Damage;
using DisasterSim.Disasters;
using DisasterSim.Environment;
using DisasterSim.Utilities;

namespace DisasterSim.UI
{
    /// <summary>
    /// Bridges the UI Toolkit HUD (MainHUD.uxml) to every gameplay manager: construction
    /// tools, disaster triggers + intensity sliders, environment sliders, simulation
    /// transport controls, the damage analysis readout, component inspector, experiment
    /// snapshots, save/load and the visualization mode dropdown.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private const string DefaultSaveSlot = "AutoSave";

        private UIDocument document;
        private VisualElement root;

        // Top bar
        private Label statusLabel, timeLabel, topTempLabel, topHumidityLabel, disasterLabel, damageLabel;

        // Left panel
        private Label activeToolLabel, storyLabel;
        private DropdownField materialDropdown, presetDropdown;

        // Right panel — disasters + damage analysis
        private Slider magnitudeSlider, waveHeightSlider;
        private Label magnitudeValue, waveValue;
        private Label integrityLabel, failedLabel, stressLabel, displacementLabel, stabilityLabel;
        private DropdownField vizDropdown;

        // Bottom panel — environment + sim controls
        private Slider temperatureSlider, humiditySlider, windSlider, waterLevelSlider;
        private Label tempValueLabel, humidityValueLabel, windValueLabel, waterValueLabel;
        private Button btnPause, btnSpeed;
        private Button btnEarthquake, btnTsunami, btnWindstorm, btnStopDisaster;

        // Inspector / experiment
        private Label inspectorContent, experimentContent;

        private PresetLoader presetLoader;
        private DisasterSim.Core.CameraController cameraController;

        private void Awake()
        {
            Instance = this;
            document = GetComponent<UIDocument>();
            root = document.rootVisualElement;

            CacheElements();
            WireConstructionButtons();
            WireDisasterControls();
            WireEnvironmentControls();
            WireSimControls();
            WireVisualizationDropdown();

            SimulationEvents.OnComponentSelected += HandleComponentSelected;
            SimulationEvents.OnComponentDeselected += HandleComponentDeselected;
            SimulationEvents.OnExperimentSnapshotTaken += RefreshExperimentPanel;
        }

        private void OnDestroy()
        {
            SimulationEvents.OnComponentSelected -= HandleComponentSelected;
            SimulationEvents.OnComponentDeselected -= HandleComponentDeselected;
            SimulationEvents.OnExperimentSnapshotTaken -= RefreshExperimentPanel;
        }

        private void Start()
        {
            // Deferred to Start so other managers' Awake (which populate their own
            // Instance / material lists) is guaranteed to have already run.
            WireMaterialDropdown();
            presetLoader = FindFirstObjectByType<PresetLoader>();
            WirePresetDropdown();

            cameraController = FindFirstObjectByType<DisasterSim.Core.CameraController>();
            WireCameraControls();
        }

        private void WireCameraControls()
        {
            Bind("btn-camera-reset", () => cameraController?.ResetCamera());
            Bind("btn-camera-focus", () => cameraController?.FocusOnSelectedComponent());

            var cinematicToggle = root.Q<Toggle>("toggle-cinematic");
            if (cinematicToggle != null && cameraController != null)
                cinematicToggle.RegisterValueChangedCallback(evt => cameraController.CinematicEnabled = evt.newValue);
        }

        private void Update()
        {
            RefreshTopBar();
            RefreshDamagePanel();
            RefreshStoryLabel();
            RefreshInspector();
            RefreshDisasterButtons();
        }

        // ---------------------------------------------------------------
        // Element lookup
        // ---------------------------------------------------------------

        private void CacheElements()
        {
            statusLabel = root.Q<Label>("status-label");
            timeLabel = root.Q<Label>("time-label");
            topTempLabel = root.Q<Label>("temp-label");
            topHumidityLabel = root.Q<Label>("humidity-label");
            disasterLabel = root.Q<Label>("disaster-label");
            damageLabel = root.Q<Label>("damage-label");

            activeToolLabel = root.Q<Label>("active-tool-label");
            storyLabel = root.Q<Label>("story-label");
            materialDropdown = root.Q<DropdownField>("material-dropdown");
            presetDropdown = root.Q<DropdownField>("preset-dropdown");

            magnitudeSlider = root.Q<Slider>("magnitude-slider");
            magnitudeValue = root.Q<Label>("magnitude-value");
            waveHeightSlider = root.Q<Slider>("wave-height-slider");
            waveValue = root.Q<Label>("wave-value");
            integrityLabel = root.Q<Label>("integrity-label");
            failedLabel = root.Q<Label>("failed-label");
            stressLabel = root.Q<Label>("stress-label");
            displacementLabel = root.Q<Label>("displacement-label");
            stabilityLabel = root.Q<Label>("stability-label");
            vizDropdown = root.Q<DropdownField>("viz-dropdown");

            temperatureSlider = root.Q<Slider>("temperature-slider");
            tempValueLabel = root.Q<Label>("temp-value");
            humiditySlider = root.Q<Slider>("humidity-slider");
            humidityValueLabel = root.Q<Label>("humidity-value");
            windSlider = root.Q<Slider>("wind-slider");
            windValueLabel = root.Q<Label>("wind-value");
            waterLevelSlider = root.Q<Slider>("water-level-slider");
            waterValueLabel = root.Q<Label>("water-value");

            btnPause = root.Q<Button>("btn-pause");
            btnSpeed = root.Q<Button>("btn-speed");

            btnEarthquake = root.Q<Button>("btn-earthquake");
            btnTsunami = root.Q<Button>("btn-tsunami");
            btnWindstorm = root.Q<Button>("btn-windstorm");
            btnStopDisaster = root.Q<Button>("btn-stop-disaster");

            inspectorContent = root.Q<Label>("inspector-content");
            experimentContent = root.Q<Label>("experiment-content");
        }

        // ---------------------------------------------------------------
        // Construction (left panel)
        // ---------------------------------------------------------------

        private void WireConstructionButtons()
        {
            Bind("btn-add-foundation", () => SetTool(BuildTool.PlaceFoundation));
            Bind("btn-add-column", () => SetTool(BuildTool.PlaceColumn));
            Bind("btn-add-beam", () => SetTool(BuildTool.PlaceBeam));
            Bind("btn-add-wall", () => SetTool(BuildTool.PlaceWall));
            Bind("btn-add-floor", () => SetTool(BuildTool.PlaceFloor));
            Bind("btn-add-roof", () => SetTool(BuildTool.PlaceRoof));
            Bind("btn-delete", () => SetTool(BuildTool.Delete));
            Bind("btn-move", () => SetTool(BuildTool.Move));
            Bind("btn-rotate", () => SetTool(BuildTool.Rotate));
        }

        private void WireMaterialDropdown()
        {
            if (materialDropdown == null || BuildingManager.Instance == null) return;

            var names = BuildingManager.Instance.AvailableMaterials
                .Where(m => m != null)
                .Select(m => m.MaterialDisplayName).ToList();
            materialDropdown.choices = names;
            if (names.Count > 0) materialDropdown.SetValueWithoutNotify(names[0]);

            materialDropdown.RegisterValueChangedCallback(evt =>
                BuildingManager.Instance.SetMaterialByName(evt.newValue));
        }

        private void WirePresetDropdown()
        {
            if (presetDropdown == null) return;

            var presets = presetLoader != null ? presetLoader.Presets : null;
            var names = presets != null
                ? presets.Where(p => p != null).Select(p => p.PresetName).ToList()
                : new System.Collections.Generic.List<string>();

            presetDropdown.choices = names;
            if (names.Count > 0) presetDropdown.SetValueWithoutNotify(names[0]);

            Bind("btn-load-preset", () =>
            {
                if (presetLoader == null || string.IsNullOrEmpty(presetDropdown.value)) return;
                presetLoader.LoadPresetByName(presetDropdown.value);
            });
        }

        private void SetTool(BuildTool tool)
        {
            if (BuildingManager.Instance == null) return;

            // Clicking the already-active tool's button toggles it back to None.
            var newTool = BuildingManager.Instance.CurrentTool == tool ? BuildTool.None : tool;
            BuildingManager.Instance.SetTool(newTool);
            if (activeToolLabel != null) activeToolLabel.text = "TOOL: " + newTool.ToString().ToUpperInvariant();
        }

        private void RefreshStoryLabel()
        {
            if (storyLabel == null || BuildingManager.Instance == null) return;
            storyLabel.text = $"STORY: {BuildingManager.Instance.CurrentStory} (Ctrl+Scroll)";
        }

        // ---------------------------------------------------------------
        // Disaster controls (right panel)
        // ---------------------------------------------------------------

        private void WireDisasterControls()
        {
            if (magnitudeSlider != null)
                magnitudeSlider.RegisterValueChangedCallback(evt =>
                {
                    if (magnitudeValue != null) magnitudeValue.text = evt.newValue.ToString("0.0");
                });

            if (waveHeightSlider != null)
                waveHeightSlider.RegisterValueChangedCallback(evt =>
                {
                    if (waveValue != null) waveValue.text = evt.newValue.ToString("0.0") + " m";
                });

            Bind("btn-earthquake", () =>
            {
                if (DisasterManager.Instance == null) return;
                float mag = magnitudeSlider != null ? magnitudeSlider.value : 5f;
                DisasterManager.Instance.TriggerEarthquake(mag);
            });

            Bind("btn-tsunami", () =>
            {
                if (DisasterManager.Instance == null) return;
                float height = waveHeightSlider != null ? waveHeightSlider.value : 5f;
                DisasterManager.Instance.TriggerTsunami(height);
            });

            Bind("btn-windstorm", () =>
            {
                if (DisasterManager.Instance == null) return;
                float wind = windSlider != null ? windSlider.value : 20f;
                DisasterManager.Instance.TriggerWindstorm(wind);
            });

            Bind("btn-stop-disaster", () => DisasterManager.Instance?.StopDisaster());
        }

        /// <summary>Only one disaster can run at a time (DisasterManager ignores a trigger
        /// while another is active). Grey out the other trigger buttons while one is running
        /// so that's visible instead of the click silently doing nothing.</summary>
        private void RefreshDisasterButtons()
        {
            bool active = DisasterManager.Instance != null && DisasterManager.Instance.IsDisasterActive;
            if (btnEarthquake != null) btnEarthquake.SetEnabled(!active);
            if (btnTsunami != null) btnTsunami.SetEnabled(!active);
            if (btnWindstorm != null) btnWindstorm.SetEnabled(!active);
            if (btnStopDisaster != null) btnStopDisaster.SetEnabled(active);
        }

        private void RefreshDamagePanel()
        {
            if (DamageManager.Instance == null) return;
            var dm = DamageManager.Instance;

            if (damageLabel != null) damageLabel.text = $"DAMAGE: {dm.OverallDamagePercent:0}%";
            if (integrityLabel != null) integrityLabel.text = $"INTEGRITY: {dm.StructuralIntegrityPercent:0}%";
            if (failedLabel != null) failedLabel.text = $"FAILED: {dm.FailedComponentCount} / {dm.TotalComponentCount}";
            if (stressLabel != null) stressLabel.text = $"MAX STRESS: {dm.MaxStressMPa:0} MPa";
            if (displacementLabel != null) displacementLabel.text = $"MAX DISP: {dm.MaxDisplacement:0.00} m";
            if (stabilityLabel != null) stabilityLabel.text = $"STABILITY: {dm.StabilityLabel}";
        }

        private void WireVisualizationDropdown()
        {
            if (vizDropdown == null) return;
            var names = System.Enum.GetNames(typeof(VisualizationMode)).ToList();
            vizDropdown.choices = names;
            vizDropdown.SetValueWithoutNotify(names[0]);
            vizDropdown.RegisterValueChangedCallback(evt =>
            {
                if (DamageVisualizationManager.Instance == null) return;
                var mode = (VisualizationMode)System.Enum.Parse(typeof(VisualizationMode), evt.newValue);
                DamageVisualizationManager.Instance.SetMode(mode);
            });
        }

        // ---------------------------------------------------------------
        // Environment controls (bottom panel)
        // ---------------------------------------------------------------

        private void WireEnvironmentControls()
        {
            if (temperatureSlider != null)
                temperatureSlider.RegisterValueChangedCallback(evt =>
                {
                    if (tempValueLabel != null) tempValueLabel.text = $"{evt.newValue:0}°C";
                    EnvironmentManager.Instance?.SetTemperature(evt.newValue);
                });

            if (humiditySlider != null)
                humiditySlider.RegisterValueChangedCallback(evt =>
                {
                    if (humidityValueLabel != null) humidityValueLabel.text = $"{evt.newValue:0}%";
                    EnvironmentManager.Instance?.SetHumidity(evt.newValue);
                });

            if (windSlider != null)
                windSlider.RegisterValueChangedCallback(evt =>
                {
                    if (windValueLabel != null) windValueLabel.text = $"{evt.newValue:0} m/s";
                    EnvironmentManager.Instance?.SetWindSpeed(evt.newValue);
                });

            if (waterLevelSlider != null)
                waterLevelSlider.RegisterValueChangedCallback(evt =>
                {
                    if (waterValueLabel != null) waterValueLabel.text = $"{evt.newValue:0.0} m";
                    EnvironmentManager.Instance?.SetWaterLevel(evt.newValue);
                    WaterSurface.Instance?.SetWaterLevel(evt.newValue);
                });
        }

        // ---------------------------------------------------------------
        // Simulation transport controls (bottom panel)
        // ---------------------------------------------------------------

        private void WireSimControls()
        {
            Bind("btn-start", () => SimulationManager.Instance?.StartSimulation());

            Bind("btn-pause", () =>
            {
                var sm = SimulationManager.Instance;
                if (sm == null) return;
                if (sm.State == SimulationState.Paused)
                {
                    sm.ResumeSimulation();
                    if (btnPause != null) btnPause.text = "Pause";
                }
                else
                {
                    sm.PauseSimulation();
                    if (btnPause != null) btnPause.text = "Resume";
                }
            });

            Bind("btn-speed", () =>
            {
                var sm = SimulationManager.Instance;
                if (sm == null) return;
                sm.CycleSpeed();
                if (btnSpeed != null) btnSpeed.text = $"Speed {sm.SimulationSpeed:0.##}x";
            });

            Bind("btn-reset", () => SimulationManager.Instance?.ResetSimulation());

            Bind("btn-save", () => SaveLoadManager.Instance?.SaveDesign(DefaultSaveSlot));
            Bind("btn-load", () => SaveLoadManager.Instance?.LoadDesign(DefaultSaveSlot));

            Bind("btn-snap-a", () => ExperimentManager.Instance?.RecordDesignA());
            Bind("btn-snap-b", () => ExperimentManager.Instance?.RecordDesignB());
        }

        private void RefreshTopBar()
        {
            var sm = SimulationManager.Instance;
            var env = EnvironmentManager.Instance;

            if (sm != null)
            {
                if (statusLabel != null) statusLabel.text = "STATUS: " + sm.State.ToString().ToUpperInvariant();
                if (timeLabel != null)
                {
                    int totalSeconds = Mathf.FloorToInt(sm.SimulationTime);
                    timeLabel.text = $"TIME: {totalSeconds / 60:00}:{totalSeconds % 60:00}";
                }
                if (disasterLabel != null) disasterLabel.text = "DISASTER: " + sm.ActiveDisaster;
            }

            if (env != null)
            {
                if (topTempLabel != null) topTempLabel.text = $"TEMP: {env.Temperature:0}°C";
                if (topHumidityLabel != null) topHumidityLabel.text = $"HUMIDITY: {env.Humidity:0}%";
            }
        }

        // ---------------------------------------------------------------
        // Component inspector
        // ---------------------------------------------------------------

        private void HandleComponentSelected(BuildingComponent comp) => RefreshInspector();
        private void HandleComponentDeselected()
        {
            if (inspectorContent != null) inspectorContent.text = "Click a component to inspect";
        }

        private void RefreshInspector()
        {
            if (inspectorContent == null || BuildingManager.Instance == null) return;
            var comp = BuildingManager.Instance.SelectedComponent;
            if (comp == null) return;

            string matName = comp.Material != null ? comp.Material.MaterialDisplayName : "None";
            inspectorContent.text =
                $"{comp.ComponentType.ToString().ToUpperInvariant()} " +
                $"[{comp.GridCell.x},{comp.GridCell.y}] S{comp.StoryIndex}\n" +
                $"Material: {matName}\n" +
                $"Mass: {comp.Mass:0.0} kg\n" +
                $"Stress: {comp.CurrentStressMPa:0.0} / {comp.CompressiveStrength:0.0} MPa\n" +
                $"Strain: {comp.CurrentStrain:0.000}\n" +
                $"Displacement: {comp.CurrentDisplacement:0.000} m\n" +
                $"Damage: {comp.DamagePercent * 100f:0}%\n" +
                $"Status: {comp.Status.ToString().ToUpperInvariant()}";
        }

        // ---------------------------------------------------------------
        // Experiment comparison
        // ---------------------------------------------------------------

        private void RefreshExperimentPanel()
        {
            if (experimentContent == null || ExperimentManager.Instance == null) return;
            var em = ExperimentManager.Instance;

            if (!em.HasDesignA && !em.HasDesignB)
            {
                experimentContent.text = "Record snapshots with Snap A / Snap B";
                return;
            }

            string aText = em.HasDesignA
                ? $"A: Damage {em.DesignA.OverallDamage:0}%  Failed {em.DesignA.FailedComponents}/{em.DesignA.TotalComponents}  MaxDisp {em.DesignA.MaxDisplacement:0.00}m"
                : "A: (not recorded)";
            string bText = em.HasDesignB
                ? $"B: Damage {em.DesignB.OverallDamage:0}%  Failed {em.DesignB.FailedComponents}/{em.DesignB.TotalComponents}  MaxDisp {em.DesignB.MaxDisplacement:0.00}m"
                : "B: (not recorded)";

            string reductionText = em.HasDesignA && em.HasDesignB
                ? $"\nDamage reduction (A→B): {em.GetDamageReduction() * 100f:0}%"
                : "";

            experimentContent.text = $"{aText}\n{bText}{reductionText}";
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private void Bind(string buttonName, System.Action onClick)
        {
            var button = root.Q<Button>(buttonName);
            if (button != null) button.clicked += onClick;
        }

        /// <summary>True if the pointer is currently over a Button, Slider, DropdownField or
        /// other interactive element (not the transparent viewport passthrough areas).</summary>
        public bool IsPointerOverInteractiveUI()
        {
            var mouse = Mouse.current;
            if (mouse == null || root?.panel == null) return false;

            Vector2 screenPos = mouse.position.ReadValue();
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, screenPos);
            VisualElement picked = root.panel.Pick(panelPos);
            return picked != null;
        }
    }
}

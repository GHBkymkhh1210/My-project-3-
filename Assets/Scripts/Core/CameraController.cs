using UnityEngine;
using UnityEngine.InputSystem;

namespace DisasterSim.Core
{
    /// <summary>
    /// Free-look orbit camera used to inspect the building/grid from any angle.
    /// Left-drag = orbit, Right-drag (or Shift+Left-drag) = pan, Scroll = zoom.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Vector3 focusPoint = new Vector3(0f, 0f, 0f);

        [Header("Orbit")]
        [SerializeField] private float orbitSpeed = 150f;
        [SerializeField] private float minPitch = 5f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 10f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 80f;

        [Header("Pan")]
        [SerializeField] private float panSpeed = 0.5f;

        private float yaw = 45f;
        private float pitch = 35f;
        private float distance = 25f;

        private Vector2 lastMousePosition;

        /// <summary>When enabled, the camera automatically pulls back to frame the whole
        /// building the moment a disaster starts (spec section 19: optional, never forced
        /// on the user - the toggle lives in the Camera panel).</summary>
        public bool CinematicEnabled { get; set; }

        private void Awake()
        {
            DisasterSim.Core.SimulationEvents.OnDisasterStarted += HandleDisasterStarted;
        }

        private void OnDestroy()
        {
            DisasterSim.Core.SimulationEvents.OnDisasterStarted -= HandleDisasterStarted;
        }

        private void HandleDisasterStarted(string disasterName)
        {
            if (!CinematicEnabled) return;
            FocusOnBuilding();
        }

        private void Start()
        {
            UpdateCameraTransform();
        }

        private void Update()
        {
            HandleOrbit();
            HandlePan();
            HandleZoom();
            UpdateCameraTransform();
        }

        private void HandleOrbit()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return;

            bool shiftHeld = keyboard != null && keyboard.shiftKey.isPressed;

            if (mouse.leftButton.wasPressedThisFrame) lastMousePosition = mouse.position.ReadValue();

            if (mouse.leftButton.isPressed && !shiftHeld)
            {
                Vector2 current = mouse.position.ReadValue();
                Vector2 delta = current - lastMousePosition;
                yaw += delta.x * orbitSpeed * Time.deltaTime * 0.1f;
                pitch -= delta.y * orbitSpeed * Time.deltaTime * 0.1f;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                lastMousePosition = current;
            }
        }

        private void HandlePan()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return;

            bool shiftHeld = keyboard != null && keyboard.shiftKey.isPressed;
            bool panning = mouse.rightButton.isPressed || (mouse.leftButton.isPressed && shiftHeld);

            if (mouse.rightButton.wasPressedThisFrame) lastMousePosition = mouse.position.ReadValue();

            if (panning)
            {
                Vector2 current = mouse.position.ReadValue();
                Vector2 delta = current - lastMousePosition;
                Vector3 right = transform.right;
                Vector3 up = transform.up;
                focusPoint -= right * delta.x * panSpeed * Time.deltaTime;
                focusPoint -= up * delta.y * panSpeed * Time.deltaTime;
                lastMousePosition = current;
            }
        }

        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                distance -= scroll * zoomSpeed * 0.01f;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }
        }

        private void UpdateCameraTransform()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = focusPoint + rotation * new Vector3(0f, 0f, -distance);
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Reset the camera to the default framing.</summary>
        public void ResetCamera()
        {
            focusPoint = Vector3.zero;
            yaw = 45f;
            pitch = 35f;
            distance = 25f;
        }

        /// <summary>Focus the camera on a specific world-space point.</summary>
        public void FocusOn(Vector3 worldPoint, float newDistance = -1f)
        {
            focusPoint = worldPoint;
            if (newDistance > 0f) distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
        }

        /// <summary>Frames the currently selected building component (spec section 19:
        /// "Focus selected component"). No-op if nothing is selected.</summary>
        public void FocusOnSelectedComponent()
        {
            var comp = DisasterSim.Building.BuildingManager.Instance != null
                ? DisasterSim.Building.BuildingManager.Instance.SelectedComponent
                : null;
            if (comp != null) FocusOn(comp.transform.position, 6f);
        }

        /// <summary>Pulls back to frame the whole building's bounding volume. Falls back to
        /// the default reset framing if nothing has been built yet.</summary>
        public void FocusOnBuilding()
        {
            var bm = DisasterSim.Building.BuildingManager.Instance;
            if (bm == null || bm.AllComponents.Count == 0) { ResetCamera(); return; }

            Bounds bounds = new Bounds(bm.AllComponents[0].transform.position, Vector3.zero);
            foreach (var comp in bm.AllComponents)
                if (comp != null) bounds.Encapsulate(comp.transform.position);

            float framingDistance = Mathf.Clamp(bounds.size.magnitude * 1.4f, minDistance, maxDistance);
            FocusOn(bounds.center, framingDistance);
        }
    }
}

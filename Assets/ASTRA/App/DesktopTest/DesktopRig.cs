using Astra.XR.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Astra.DesktopTest
{
    /// <summary>
    /// TEMPORARY seated desktop/WebGL rig: mouse look while the cursor is locked, crosshair ray, click to activate.
    /// No locomotion (NFR-UX-01). Replaced on device by XR & Interaction's AstraXRRig.
    /// OWNER: Integration Lead (test tooling).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DesktopRig : MonoBehaviour
    {
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private float maxYaw = 100f, maxPitch = 70f;
        [SerializeField] private float rayLength = 1.5f;

        private Camera _cam;
        private float _yaw, _pitch;
        private Quaternion _rest;
        private TestTarget _hovered;
        private IGazeTarget _hoveredUi;   // non-TestTarget gaze targets (debrief RETRY / HUB / NEXT LEVEL buttons)

        public bool Locked => Cursor.lockState == CursorLockMode.Locked;
        public TestTarget Hovered => _hovered;

        private void Awake() { _cam = GetComponent<Camera>(); _rest = transform.localRotation; }

        public void Recenter() { _yaw = 0f; _pitch = 0f; transform.localRotation = _rest; }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (!Locked)
            {
                // Free cursor: UI gaze buttons (debrief) can be clicked at the mouse position.
                IGazeTarget ui = null;
                if (!DesktopHud.PointerOverHud) ui = UiUnder(_cam.ScreenPointToRay(mouse.position.ReadValue()), 4f);
                SetHovered(null);
                SetHoveredUi(ui);
                if (mouse.leftButton.wasPressedThisFrame && !DesktopHud.PointerOverHud)
                {
                    if (ui != null) ui.ActivateFromGaze();
                    else if (!DesktopHud.WantsFreeCursor) Cursor.lockState = CursorLockMode.Locked;   // browsers require a click for pointer lock
                }
                return;
            }

            Vector2 d = mouse.delta.ReadValue() * sensitivity;
            _yaw = Mathf.Clamp(_yaw + d.x, -maxYaw, maxYaw);
            _pitch = Mathf.Clamp(_pitch - d.y, -maxPitch, maxPitch);
            transform.localRotation = _rest * Quaternion.Euler(_pitch, _yaw, 0f);

            var ray = new Ray(_cam.transform.position, _cam.transform.forward);
            TestTarget hit = null;
            IGazeTarget uiHit = null;
            if (Physics.Raycast(ray, out var info, rayLength))
            {
                hit = info.collider.GetComponentInParent<TestTarget>();
                if (hit == null) uiHit = GazeTargetOf(info.collider);
            }
            SetHovered(hit);
            SetHoveredUi(uiHit);

            if (mouse.leftButton.wasPressedThisFrame && !DesktopHud.PointerOverHud)
            {
                if (_hovered != null) _hovered.Activate();
                else if (_hoveredUi != null) _hoveredUi.ActivateFromGaze();
            }
        }

        private static IGazeTarget GazeTargetOf(Collider c)
        {
            foreach (var mb in c.GetComponentsInParent<MonoBehaviour>())
                if (mb is IGazeTarget g && !(mb is TestTarget) && g.IsGazeEnabled) return g;
            return null;
        }

        // First hit only: a UI gaze target that is not a TestTarget.
        private static IGazeTarget UiUnder(Ray ray, float length)
        {
            if (!Physics.Raycast(ray, out var info, length)) return null;
            if (info.collider.GetComponentInParent<TestTarget>() != null) return null;
            return GazeTargetOf(info.collider);
        }

        private void SetHoveredUi(IGazeTarget t)
        {
            if (t == _hoveredUi) return;
            if (_hoveredUi is Object o && o != null) _hoveredUi.SetGazeHover(false);   // skip if destroyed with the debrief
            _hoveredUi = t;
            if (_hoveredUi != null) _hoveredUi.SetGazeHover(true);
        }

        private void SetHovered(TestTarget t)
        {
            if (t == _hovered) return;
            if (_hovered != null) _hovered.SetHover(false);
            _hovered = t;
            if (_hovered != null) _hovered.SetHover(true);
        }
    }
}

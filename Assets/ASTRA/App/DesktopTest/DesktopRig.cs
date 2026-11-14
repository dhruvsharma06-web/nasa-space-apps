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
                // First click into the view locks the cursor (browsers require a click for pointer lock).
                if (mouse.leftButton.wasPressedThisFrame && !DesktopHud.PointerOverHud) Cursor.lockState = CursorLockMode.Locked;
                SetHovered(null);
                return;
            }

            Vector2 d = mouse.delta.ReadValue() * sensitivity;
            _yaw = Mathf.Clamp(_yaw + d.x, -maxYaw, maxYaw);
            _pitch = Mathf.Clamp(_pitch - d.y, -maxPitch, maxPitch);
            transform.localRotation = _rest * Quaternion.Euler(_pitch, _yaw, 0f);

            var ray = new Ray(_cam.transform.position, _cam.transform.forward);
            TestTarget hit = null;
            if (Physics.Raycast(ray, out var info, rayLength)) hit = info.collider.GetComponentInParent<TestTarget>();
            SetHovered(hit);

            if (mouse.leftButton.wasPressedThisFrame && _hovered != null) _hovered.Activate();
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

using UnityEngine;
using UnityEngine.XR;
using XRUsages = UnityEngine.XR.CommonUsages;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Astra.XR.Phone
{
    /// <summary>
    /// Drives the head camera rotation, in priority order:
    /// <list type="number">
    /// <item><b>XR</b> (Cardboard XR Plugin, or any provider): the tracked head device's centre-eye pose.</item>
    /// <item><b>Gyro "magic window"</b> fallback: the Input System <c>AttitudeSensor</c> (no package change needed).</item>
    /// <item><b>Editor/desktop</b>: right-drag mouse look, clamped.</item>
    /// </list>
    /// Only the head moves the camera (no locomotion, no artificial turning). <see cref="RecenterYaw"/> rotates the
    /// <c>yawOffset</c> parent so the current view direction becomes forward again.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhoneHeadPose : MonoBehaviour
    {
        public enum Source { None, XR, Gyro, Mouse }

        [SerializeField] private Transform yawOffset;
        [SerializeField] private bool useGyroFallback = true;
        [SerializeField] private bool mouseLookInEditor = true;
        [SerializeField] private Vector3 gyroCorrectionEuler = new Vector3(90f, 0f, 0f);
        [SerializeField, Range(0.02f, 1f)] private float mouseDegreesPerPixel = 0.15f;
        [SerializeField, Range(0f, 90f)] private float maxPitch = 70f;

        private float mouseYaw;
        private float mousePitch;
        private bool gyroRecentered;

        public Source Current { get; private set; }
        public bool IsXR => Current == Source.XR;

        public void Configure(Transform yawParent) => yawOffset = yawParent;

        /// <summary>Off when another desktop rig already drives mouse look on this camera.</summary>
        public void SetMouseLook(bool on) => mouseLookInEditor = on;

        private void OnEnable()
        {
            Application.onBeforeRender += Apply;
#if ENABLE_INPUT_SYSTEM
            if (useGyroFallback && AttitudeSensor.current != null && !AttitudeSensor.current.enabled)
                InputSystem.EnableDevice(AttitudeSensor.current);
#endif
        }

        private void OnDisable() => Application.onBeforeRender -= Apply;
        private void Update() => Apply();

        private void Apply()
        {
            if (TryXR())
            {
                Current = Source.XR;
                return;
            }
            if (useGyroFallback && TryGyro())
            {
                Current = Source.Gyro;
                return;
            }
            if (mouseLookInEditor && TryMouse())
            {
                Current = Source.Mouse;
                return;
            }
            Current = Source.None;
        }

        private bool TryXR()
        {
            if (!XRSettings.isDeviceActive)
                return false;
            var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!device.isValid || !device.TryGetFeatureValue(XRUsages.isTracked, out var tracked) || !tracked)
                return false;
            if (device.TryGetFeatureValue(XRUsages.centerEyeRotation, out var rotation) ||
                device.TryGetFeatureValue(XRUsages.deviceRotation, out rotation))
                transform.localRotation = rotation;
            if (device.TryGetFeatureValue(XRUsages.centerEyePosition, out var position) ||
                device.TryGetFeatureValue(XRUsages.devicePosition, out position))
                transform.localPosition = position;
            return true;
        }

        private bool TryGyro()
        {
#if ENABLE_INPUT_SYSTEM
            var sensor = AttitudeSensor.current;
            if (sensor == null || !sensor.enabled)
                return false;
            var q = sensor.attitude.ReadValue();
            if (q == default(Quaternion))
                return false;
            // Right-handed device attitude → Unity left-handed camera. Verify gyroCorrectionEuler on the phone
            // (landscape left); it is serialized so it can be tuned without code changes.
            transform.localRotation = Quaternion.Euler(gyroCorrectionEuler) * new Quaternion(q.x, q.y, -q.z, -q.w);
            if (!gyroRecentered)
            {
                gyroRecentered = true;
                RecenterYaw(); // start facing the panels
            }
            return true;
#else
            return false;
#endif
        }

        private bool TryMouse()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null || Touchscreen.current != null && Application.isMobilePlatform)
                return false;
            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue() * mouseDegreesPerPixel;
                mouseYaw = Mathf.Clamp(mouseYaw + delta.x, -100f, 100f);
                mousePitch = Mathf.Clamp(mousePitch - delta.y, -maxPitch, maxPitch);
            }
            transform.localRotation = Quaternion.Euler(mousePitch, mouseYaw, 0f);
            return true;
#else
            return false;
#endif
        }

        /// <summary>Makes the current view direction the new forward (yaw only; pitch and roll stay with the head).</summary>
        public void RecenterYaw()
        {
            if (Current == Source.Mouse || Current == Source.None)
            {
                mouseYaw = 0f;
                mousePitch = 0f;
                transform.localRotation = Quaternion.identity;
                return;
            }
            if (yawOffset == null)
                return;
            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                return;
            var worldYaw = Quaternion.LookRotation(forward.normalized).eulerAngles.y;
            var parentYaw = yawOffset.parent != null ? yawOffset.parent.eulerAngles.y : 0f;
            // Rotate the offset so that the head's world yaw equals the rig's (parent) yaw.
            yawOffset.rotation = Quaternion.Euler(0f, parentYaw - worldYaw, 0f) * yawOffset.rotation;
        }
    }
}

using UnityEngine;
using UnityEngine.XR;

namespace Astra.XR.Rig
{
    /// <summary>
    /// Drives the rig camera from the XR head device (center eye) whenever a headset is tracking.
    /// Pose is local to the tracking-space parent, so the seated rig origin stays the reference.
    /// Without a tracked headset the camera keeps its desktop pose.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeadPoseDriver : MonoBehaviour
    {
        private Vector3 desktopPosition;
        private Quaternion desktopRotation;
        private bool wasTracking;

        /// <summary>True while a headset reports a tracked head pose.</summary>
        public bool IsTracking { get; private set; }

        private void Awake()
        {
            desktopPosition = transform.localPosition;
            desktopRotation = transform.localRotation;
        }

        private void OnEnable() => Application.onBeforeRender += Apply;
        private void OnDisable() => Application.onBeforeRender -= Apply;
        private void Update() => Apply();

        private void Apply()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            var tracked = head.isValid && head.TryGetFeatureValue(CommonUsages.isTracked, out var valid) && valid &&
                          XRSettings.isDeviceActive;
            if (tracked)
            {
                if (head.TryGetFeatureValue(CommonUsages.centerEyePosition, out var position) ||
                    head.TryGetFeatureValue(CommonUsages.devicePosition, out position))
                    transform.localPosition = position;
                if (head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out var rotation) ||
                    head.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
                    transform.localRotation = rotation;
            }
            else if (wasTracking)
            {
                // Returning to the desktop window restores the seated desktop view.
                transform.localPosition = desktopPosition;
                transform.localRotation = desktopRotation;
            }
            wasTracking = tracked;
            IsTracking = tracked;
        }
    }
}

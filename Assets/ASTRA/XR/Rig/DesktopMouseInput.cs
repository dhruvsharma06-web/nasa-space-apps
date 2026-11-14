using Astra.XR.Input;
using UnityEngine;
using UnityEngine.Events;

namespace Astra.XR.Rig
{
    /// <summary>
    /// Desktop route used before the headset session starts (or without a headset): reports as
    /// <c>simulator</c>. Left click selects/activates; hold left button on a tool to grab, release to drop or place;
    /// scroll changes held distance; right-drag turns the seated view (rotation only); ESC or P opens the pause menu.
    /// World-space UI is clicked through the scene EventSystem. Disabled while a headset is tracking.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DesktopMouseInput : MonoBehaviour
    {
        [SerializeField] private RayInteractor interactor;
        [SerializeField] private InputModalityTag actor;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private HeadPoseDriver headPose;
        [SerializeField, Range(0f, 90f)] private float maxYaw = 75f;
        [SerializeField, Range(0f, 80f)] private float maxPitch = 55f;
        [SerializeField, Min(0.01f)] private float lookDegreesPerUnit = 1.5f;
        [SerializeField] private UnityEvent menuPressed = new UnityEvent();

        private Quaternion forward;
        private float yaw;
        private float pitch;
        private bool active;

        public UnityEvent onMenuPressed => menuPressed;

        public void Configure(RayInteractor ray, InputModalityTag tag, Camera camera, HeadPoseDriver head)
        {
            interactor = ray;
            actor = tag;
            viewCamera = camera;
            headPose = head;
        }

        public void ResetLook()
        {
            yaw = 0f;
            pitch = 0f;
            ApplyLook();
        }

        private void Awake()
        {
            if (interactor == null || actor == null || viewCamera == null)
            {
                Debug.LogError($"[DesktopMouseInput] {name}: assign the ray interactor, modality tag and camera.", this);
                enabled = false;
                return;
            }
            forward = viewCamera.transform.localRotation;
#if !ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogWarning("[DesktopMouseInput] Desktop testing needs Active Input Handling = Input Manager or Both.", this);
            enabled = false;
#endif
        }

        private void OnDisable()
        {
            if (interactor != null)
                interactor.Cancel();
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private void Update()
        {
            var headset = headPose != null && headPose.IsTracking;
            if (headset)
            {
                if (active)
                    interactor.Cancel();
                active = false;
                return;
            }
            if (!active)
                ApplyLook();
            active = true;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.P))
                menuPressed.Invoke();
            if (this == null || !isActiveAndEnabled)
                return;

            if (UnityEngine.Input.GetMouseButton(1))
            {
                yaw = Mathf.Clamp(yaw + UnityEngine.Input.GetAxisRaw("Mouse X") * lookDegreesPerUnit, -maxYaw, maxYaw);
                pitch = Mathf.Clamp(pitch - UnityEngine.Input.GetAxisRaw("Mouse Y") * lookDegreesPerUnit, -maxPitch, maxPitch);
                ApplyLook();
            }

            var mouse = UnityEngine.Input.mousePosition;
            var inside = mouse.x >= 0f && mouse.y >= 0f && mouse.x <= Screen.width && mouse.y <= Screen.height;
            interactor.Step(new RayInteractor.Frame
            {
                Ray = viewCamera.ScreenPointToRay(mouse),
                Valid = inside,
                SelectDown = UnityEngine.Input.GetMouseButtonDown(0),
                GrabDown = false,
                GrabHeld = UnityEngine.Input.GetMouseButton(0),
                SelectGrabs = true,
                ClickUi = false,
                SelectActor = actor,
                GrabActor = actor,
                HoldDistanceDelta = UnityEngine.Input.mouseScrollDelta.y * 0.03f
            });
        }
#endif

        private void ApplyLook()
        {
            if (viewCamera != null && (headPose == null || !headPose.IsTracking))
                viewCamera.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * forward * Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}

using System.Collections.Generic;
using Astra.XR.Input;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

namespace Astra.XR.Rig
{
    /// <summary>
    /// Phone-headset route: the head (centre-eye) ray is the pointer and the headset button tap acts on it
    /// (see <see cref="GazeTap"/>). On the Vision Aura phone headset the button is a screen tap that the runtime
    /// delivers as the hand-aim device's <c>MenuButton</c>; it is valid whenever the head is tracked, so this route
    /// works without hand detection. This component owns that button: a tap on empty space opens the pause menu.
    /// Reports <c>controller_ray</c> (CONTRACT.md §3 has no gaze modality; the gaze ray plus button is a ray route).
    ///
    /// Desktop preview: G toggles a screen-centre gaze reticle, Space taps. Those reports use the simulator tag.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GazeTapInput : MonoBehaviour
    {
        [SerializeField] private RayInteractor interactor;
        [SerializeField] private InputModalityTag headsetActor;
        [SerializeField] private InputModalityTag desktopActor;
        [SerializeField] private HeadPoseDriver headPose;
        [SerializeField] private Transform head;
        [SerializeField] private GazeReticle reticle;
        [SerializeField] private UnityEvent menuPressed = new UnityEvent();

        private readonly List<InputDevice> devices = new List<InputDevice>();
        private InputDevice buttonDevice;
        private GazeTap gaze;
        private bool wasButton;
        private bool desktopPreview;
        private bool wasActive;

        public UnityEvent onMenuPressed => menuPressed;
        public bool IsActive { get; private set; }
        public bool DesktopPreview => desktopPreview;

        public void Configure(RayInteractor ray, InputModalityTag headset, InputModalityTag desktop, HeadPoseDriver pose,
            Transform headTransform, GazeReticle gazeReticle)
        {
            interactor = ray;
            headsetActor = headset;
            desktopActor = desktop;
            headPose = pose;
            head = headTransform;
            reticle = gazeReticle;
        }

        private void Awake()
        {
            if (interactor == null || headsetActor == null || desktopActor == null || head == null)
            {
                Debug.LogError($"[GazeTapInput] {name}: assign the ray interactor, actors and head transform.", this);
                enabled = false;
                return;
            }
            gaze = new GazeTap(interactor);
        }

        private void OnDisable()
        {
            gaze?.Cancel();
            IsActive = false;
            wasActive = false;
            if (reticle != null)
                reticle.Hide();
        }

        private void Update()
        {
            var headset = headPose != null && headPose.IsTracking;
            var button = ReadHeadsetButton();
            var tap = false;
            Component actor = headsetActor;
            if (headset)
            {
                tap = button && !wasButton;
            }
            else
            {
                actor = desktopActor;
#if ENABLE_LEGACY_INPUT_MANAGER
                if (UnityEngine.Input.GetKeyDown(KeyCode.G))
                    desktopPreview = !desktopPreview;
                tap = desktopPreview && UnityEngine.Input.GetKeyDown(KeyCode.Space);
#endif
            }
            wasButton = button;

            IsActive = headset || desktopPreview;
            if (!IsActive)
            {
                if (wasActive)
                    gaze.Cancel();
                wasActive = false;
                if (reticle != null)
                    reticle.Hide();
                return;
            }
            wasActive = true;

            var ray = new Ray(head.position, head.forward);
            var outcome = gaze.Step(ray, true, tap, actor);
            if (reticle != null)
                reticle.Show(ray, gaze.HasHit, gaze.HitPoint, gaze.PointsAtTarget, gaze.Holding, tap);
            if (outcome == GazeTap.Outcome.Menu)
            {
                using (UiActionActor.Push(actor))
                    menuPressed.Invoke();
            }
        }

        private bool ReadHeadsetButton()
        {
            if (!buttonDevice.isValid && !FindButtonDevice())
                return false;
            return buttonDevice.TryGetFeatureValue(CommonUsages.menuButton, out var pressed) && pressed;
        }

        private bool FindButtonDevice()
        {
            InputDevices.GetDevices(devices);
            foreach (var candidate in devices)
            {
                if (candidate.isValid && candidate.name == HandAimInput.VisionAuraHandAim)
                {
                    buttonDevice = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}

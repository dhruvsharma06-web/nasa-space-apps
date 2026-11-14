using System.Collections.Generic;
using Astra.XR.Input;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

namespace Astra.XR.Rig
{
    /// <summary>
    /// Headset route: a camera-derived hand-aim device exposed through UnityEngine.XR InputDevices
    /// (e.g. the Vision Aura provider's "Vision Aura Hand Aim"; any tracked controller with trigger/grip works).
    /// Pinch/trigger = select and activate (reports <c>hand_pinch</c> for a hand device, otherwise <c>controller_ray</c>);
    /// fist/grip = grab (<c>hand_grab</c> / <c>controller_grip</c>); menu = pause menu unless <see cref="GazeTapInput"/>
    /// owns the headset button (the rig sets <c>handleMenuButton</c> off). Tracking loss cancels a grab.
    /// The modality is decided by the device's actual characteristics, never guessed per action.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandAimInput : MonoBehaviour
    {
        public const string VisionAuraHandAim = "Vision Aura Hand Aim";

        [SerializeField] private RayInteractor interactor;
        [SerializeField] private InputModalityTag selectActor;
        [SerializeField] private InputModalityTag grabActor;
        [SerializeField] private Transform trackingSpace;
        [SerializeField] private HeadPoseDriver headPose;
        [SerializeField] private LineRenderer rayVisual;
        [SerializeField] private Transform cursor;
        [SerializeField] private bool handleMenuButton = true;
        [SerializeField] private UnityEvent menuPressed = new UnityEvent();

        private readonly List<InputDevice> devices = new List<InputDevice>();
        private InputDevice device;
        private bool wasSelect;
        private bool wasGrab;
        private bool wasMenu;
        private bool deviceIsHand;

        public UnityEvent onMenuPressed => menuPressed;
        public bool IsTracking { get; private set; }

        public void Configure(RayInteractor ray, InputModalityTag select, InputModalityTag grab, Transform space,
            HeadPoseDriver head, LineRenderer line, Transform cursorMarker)
        {
            interactor = ray;
            selectActor = select;
            grabActor = grab;
            trackingSpace = space;
            headPose = head;
            rayVisual = line;
            cursor = cursorMarker;
        }

        /// <summary>False when another route (the phone gaze + tap route) owns the headset button.</summary>
        public bool HandleMenuButton
        {
            get => handleMenuButton;
            set => handleMenuButton = value;
        }

        private void Awake()
        {
            if (interactor == null || selectActor == null || grabActor == null || trackingSpace == null)
            {
                Debug.LogError($"[HandAimInput] {name}: assign the ray interactor, actors and tracking space.", this);
                enabled = false;
            }
            SetVisual(false, default, default);
        }

        private void OnDisable()
        {
            if (interactor != null)
                interactor.Cancel();
            SetVisual(false, default, default);
            IsTracking = false;
        }

        private void Update()
        {
            if (!device.isValid && !FindDevice())
            {
                Lost();
                return;
            }

            var tracked = device.TryGetFeatureValue(CommonUsages.isTracked, out var isTracked) && isTracked &&
                          (headPose == null || headPose.IsTracking);
            var menu = false;
            if (handleMenuButton)
                device.TryGetFeatureValue(CommonUsages.menuButton, out menu);
            if (menu && !wasMenu)
            {
                using (UiActionActor.Push(selectActor))
                    menuPressed.Invoke();
            }
            wasMenu = menu;
            if (this == null || !isActiveAndEnabled)
                return;

            if (!tracked || !device.TryGetFeatureValue(CommonUsages.devicePosition, out var position) ||
                !device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation))
            {
                Lost();
                return;
            }

            device.TryGetFeatureValue(CommonUsages.triggerButton, out var select);
            device.TryGetFeatureValue(CommonUsages.gripButton, out var grab);
            var origin = trackingSpace.TransformPoint(position);
            var direction = trackingSpace.rotation * (rotation * Vector3.forward);
            var ray = new Ray(origin, direction);
            ApplyModalities();
            IsTracking = true;

            interactor.Step(new RayInteractor.Frame
            {
                Ray = ray,
                Valid = true,
                SelectDown = select && !wasSelect,
                GrabDown = grab && !wasGrab,
                GrabHeld = grab,
                SelectGrabs = false,
                ClickUi = true,
                SelectActor = selectActor,
                GrabActor = grabActor
            });
            wasSelect = select;
            wasGrab = grab;
            var end = interactor.HasHit ? interactor.HitPoint : ray.GetPoint(interactor.MaxDistance);
            SetVisual(true, origin + direction * 0.05f, end);
            if (cursor != null)
            {
                cursor.gameObject.SetActive(interactor.HasHit);
                cursor.position = end;
            }
        }

        private void Lost()
        {
            if (IsTracking && interactor != null)
                interactor.Cancel();
            IsTracking = false;
            wasSelect = false;
            wasGrab = false;
            SetVisual(false, default, default);
        }

        private bool FindDevice()
        {
            InputDevices.GetDevices(devices);
            InputDevice fallback = default;
            foreach (var candidate in devices)
            {
                if (!candidate.isValid)
                    continue;
                if (candidate.name == VisionAuraHandAim)
                {
                    device = candidate;
                    deviceIsHand = true;
                    return true;
                }
                if (!fallback.isValid && (candidate.characteristics & InputDeviceCharacteristics.Controller) != 0)
                    fallback = candidate;
            }
            device = fallback;
            deviceIsHand = fallback.isValid && (fallback.characteristics & InputDeviceCharacteristics.HandTracking) != 0;
            return device.isValid;
        }

        private void ApplyModalities()
        {
            var select = deviceIsHand ? Astra.Core.Assessment.InputModality.HandPinch : Astra.Core.Assessment.InputModality.ControllerRay;
            var grab = deviceIsHand ? Astra.Core.Assessment.InputModality.HandGrab : Astra.Core.Assessment.InputModality.ControllerGrip;
            if (selectActor.Modality != select)
                selectActor.Configure(select);
            if (grabActor.Modality != grab && interactor.HeldTool == null)
                grabActor.Configure(grab);
        }

        private void SetVisual(bool visible, Vector3 start, Vector3 end)
        {
            if (rayVisual != null)
            {
                rayVisual.enabled = visible;
                if (visible)
                {
                    rayVisual.SetPosition(0, start);
                    rayVisual.SetPosition(1, end);
                }
            }
            if (!visible && cursor != null)
                cursor.gameObject.SetActive(false);
        }
    }
}

using System;
using Astra.Core.Assessment;
using Astra.XR.Input;
using Astra.XR.Interactables;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Astra.XR.Phone
{
    /// <summary>
    /// Phone VR gaze interaction (CONTRACT v1.4 §3, input <c>gaze</c>): a ray from the view centre picks the first
    /// collider; its <see cref="IGazeTarget"/> (if any, and enabled) is hovered.
    /// <list type="bullet">
    /// <item><b>Tap</b> (screen touch, the Cardboard/headset button, which arrives as a touch, mouse click or Space in the
    /// editor) activates the hovered target on the press.</item>
    /// <item><b>Dwell-to-select</b> (accessibility) activates after <see cref="DwellSelectSeconds"/>; on by default in L0
    /// via <see cref="GazeSettings"/>, a toggle elsewhere. <see cref="IGazeLocatable"/> targets always locate after their
    /// own dwell (1.0 s), even with dwell-to-select off.</item>
    /// <item><b>Long press</b> (≥ <see cref="LongPressSeconds"/>) recenters yaw: a scene <see cref="IRecenterHandler"/>
    /// publishes the report when present, otherwise this interactor recenters the head pose and raises one
    /// <c>recentered</c>/<c>system</c> report itself.</item>
    /// <item>A short tap on nothing raises <see cref="onEmptyTap"/> (for a pause menu, if the level wires one).</item>
    /// </list>
    /// The camera is moved only by the head (<see cref="PhoneHeadPose"/>). This component never scores or changes state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GazeInteractor : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField, Min(0.1f)] private float maxDistance = 3f;
        [SerializeField] private LayerMask layers = ~0;
        [SerializeField] private bool useGlobalDwellSetting = true;
        [SerializeField] private bool dwellSelect;
        [SerializeField, Range(0.5f, 5f)] private float dwellSelectSeconds = 1.5f;
        [SerializeField, Range(0.3f, 3f)] private float longPressSeconds = 1f;
        [SerializeField] private InputModalityTag gazeActor;
        [SerializeField] private PhoneHeadPose headPose;
        [SerializeField] private MonoBehaviour recenterHandler;
        [SerializeField] private GazeReticleView reticle;
        [SerializeField] private bool useMouseAndKeyboard = true;
        [SerializeField] private UnityEvent emptyTap = new UnityEvent();

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private IGazeTarget hovered;
        private float dwellTime;
        private bool dwellFired;
        private bool pressing;
        private bool pressConsumed;
        private float pressTime;
        private bool simulatedPress;
        private bool mouseAim;
        private Camera aimCamera;

        public UnityEvent onEmptyTap => emptyTap;
        public IGazeTarget Hovered => IsAlive(hovered) ? hovered : null;
        public bool HasHit { get; private set; }
        public Vector3 HitPoint { get; private set; }
        public float HitDistance { get; private set; }
        public float MaxDistance => maxDistance;
        public float LongPressSeconds => longPressSeconds;

        /// <summary>0–1 progress of the dwell that will fire next on the hovered target (0 when none applies).</summary>
        public float DwellProgress { get; private set; }

        public bool DwellSelectEnabled
        {
            get => useGlobalDwellSetting ? GazeSettings.DwellSelect : dwellSelect;
            set
            {
                if (useGlobalDwellSetting) GazeSettings.DwellSelect = value;
                else dwellSelect = value;
            }
        }

        public float DwellSelectSeconds
        {
            get => dwellSelectSeconds;
            set => dwellSelectSeconds = Mathf.Clamp(value, 0.5f, 5f);
        }

        /// <summary>Raised after a target was activated (tap or dwell).</summary>
        public event Action<IGazeTarget> Activated;
        /// <summary>Raised when the hovered target changes (null = nothing).</summary>
        public event Action<IGazeTarget> HoverChanged;

        public void Configure(Transform headTransform, InputModalityTag actor, PhoneHeadPose pose, GazeReticleView view)
        {
            head = headTransform;
            gazeActor = actor;
            headPose = pose;
            reticle = view;
        }

        /// <summary>Uses the global dwell toggle (true) or this component's own setting (false).</summary>
        public void UseGlobalDwellSetting(bool global) => useGlobalDwellSetting = global;

        /// <summary>Off when a desktop rig on the same camera already handles mouse clicks (no double activation).</summary>
        public void SetMouseAndKeyboard(bool on) => useMouseAndKeyboard = on;

        /// <summary>
        /// Desktop pointer mode (Hub in the browser/editor): the ray goes through the mouse cursor instead of the view
        /// centre, so a plain mouse move aims and a left click selects. Ignored on mobile and when mouse input is off.
        /// </summary>
        public void SetMouseAim(bool on) => mouseAim = on;
        public bool MouseAim => mouseAim;

        /// <summary>Test hook and accessibility switch: presses (true) or releases (false) the gaze button.</summary>
        public void SetSimulatedPress(bool down) => simulatedPress = down;

        private void Awake()
        {
            if (head == null)
                head = Camera.main != null ? Camera.main.transform : transform;
        }

        private void OnDisable()
        {
            SetHovered(null);
            pressing = false;
            if (reticle != null)
                reticle.Hide();
        }

        private void Update()
        {
            var held = ReadPressHeld();
            Tick(CurrentRay(), held && !pressing, held, Time.unscaledDeltaTime);
        }

        private Ray CurrentRay()
        {
#if ENABLE_INPUT_SYSTEM
            if (mouseAim && useMouseAndKeyboard && !Application.isMobilePlatform && Mouse.current != null)
            {
                if (aimCamera == null)
                    aimCamera = head != null ? head.GetComponent<Camera>() : null;
                if (aimCamera == null)
                    aimCamera = Camera.main;
                var position = Mouse.current.position.ReadValue();
                if (aimCamera != null && position.x >= 0f && position.y >= 0f && position.x <= Screen.width && position.y <= Screen.height)
                    return aimCamera.ScreenPointToRay(position);
            }
#endif
            return new Ray(head.position, head.forward);
        }

        /// <summary>
        /// One gaze update with explicit input (used by <see cref="Update"/> and by tests).
        /// <paramref name="pressDown"/> = the press started this frame; <paramref name="pressHeld"/> = it is down now.
        /// </summary>
        public void Tick(Ray ray, bool pressDown, bool pressHeld, float deltaTime)
        {
            var target = Pick(ray);
            SetHovered(target);
            var current = Hovered;

            // Dwell: locate targets always, everything else only with dwell-to-select on.
            DwellProgress = 0f;
            if (current != null && !dwellFired)
            {
                var need = DwellNeeded(current);
                if (!float.IsPositiveInfinity(need))
                {
                    dwellTime += Mathf.Max(0f, deltaTime);
                    DwellProgress = Mathf.Clamp01(dwellTime / need);
                    if (dwellTime >= need)
                    {
                        dwellFired = true;
                        DwellProgress = 0f;
                        Activate(current);
                    }
                }
            }

            // Tap on press; long press recenters; a short tap on nothing is an "empty tap".
            if (pressDown)
            {
                pressing = true;
                pressConsumed = false;
                pressTime = 0f;
                current = Hovered;
                if (current != null)
                {
                    pressConsumed = true;
                    dwellFired = true; // a tap completes this hover; no extra dwell activation
                    Activate(current);
                }
            }
            else if (pressing && pressHeld)
            {
                pressTime += Mathf.Max(0f, deltaTime);
                if (pressTime >= longPressSeconds && pressTime - Mathf.Max(0f, deltaTime) < longPressSeconds)
                {
                    pressConsumed = true;
                    Recenter();
                }
            }
            else if (pressing && !pressHeld)
            {
                pressing = false;
                if (!pressConsumed)
                    emptyTap.Invoke();
            }

            if (reticle != null)
                reticle.Show(ray, HasHit, HitPoint, Hovered, DwellProgress, pressing ? Mathf.Clamp01(pressTime / longPressSeconds) : 0f,
                    Hovered != null ? LabelFor(Hovered) : null);
        }

        /// <summary>Recenter yaw now (long press, pause-menu button or code).</summary>
        public void Recenter()
        {
            if (recenterHandler is IRecenterHandler handler)
            {
                if (headPose != null)
                    headPose.RecenterYaw();
                handler.TryRecenter(gazeActor != null ? gazeActor : (Component)this);
                return;
            }
            if (headPose != null)
                headPose.RecenterYaw();
            InteractionEvents.Raise(EventNames.Recentered, SystemTargets.System, InputModality.Gaze);
        }

        private float DwellNeeded(IGazeTarget target)
        {
            if (target is IGazeLocatable locatable && locatable.GazeLocateSeconds > 0f)
                return locatable.GazeLocateSeconds;
            return DwellSelectEnabled ? dwellSelectSeconds : float.PositiveInfinity;
        }

        private void Activate(IGazeTarget target)
        {
            using (UiActionActor.Push(gazeActor))
                target.ActivateFromGaze();
            Activated?.Invoke(target);
        }

        private IGazeTarget Pick(Ray ray)
        {
            HasHit = false;
            var count = Physics.RaycastNonAlloc(ray, hits, maxDistance, layers, QueryTriggerInteraction.Ignore);
            var best = -1;
            for (var i = 0; i < count; i++)
                if (best < 0 || hits[i].distance < hits[best].distance)
                    best = i;
            if (best < 0)
                return null;
            HasHit = true;
            HitPoint = hits[best].point;
            HitDistance = hits[best].distance;
            // "Take the first collider": an occluder in front blocks what is behind it.
            var target = hits[best].collider.GetComponentInParent<IGazeTarget>();
            return IsAlive(target) && target.IsGazeEnabled ? target : null;
        }

        private void SetHovered(IGazeTarget target)
        {
            var previous = Hovered;
            if (ReferenceEquals(previous, target))
                return;
            hovered = target;
            dwellTime = 0f;
            dwellFired = false;
            if (previous != null)
                previous.SetGazeHover(false);
            if (target != null)
                target.SetGazeHover(true);
            HoverChanged?.Invoke(target);
        }

        private bool ReadPressHeld()
        {
            if (simulatedPress)
                return true;
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
                return true;
            if (!useMouseAndKeyboard)
                return false;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                return true;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.isPressed)
                return true;
#endif
            return false;
        }

        /// <summary>Hover label: <see cref="IGazeLabel"/> when provided, else the targetId in CAPS without its type suffix.</summary>
        public static string LabelFor(IGazeTarget target)
        {
            if (target is IGazeLabel labelled && !string.IsNullOrWhiteSpace(labelled.GazeLabel))
                return labelled.GazeLabel;
            var id = target.TargetId ?? string.Empty;
            foreach (var suffix in new[] { "_button", "_toggle", "_dial", "_lever", "_panel", "_tool", "_socket", "_slot" })
                if (id.EndsWith(suffix, StringComparison.Ordinal) && id.Length > suffix.Length)
                {
                    id = id.Substring(0, id.Length - suffix.Length);
                    break;
                }
            return id.Replace('_', ' ').ToUpperInvariant();
        }

        private static bool IsAlive(IGazeTarget target) =>
            target is UnityEngine.Object unityObject ? unityObject != null : target != null;
    }
}

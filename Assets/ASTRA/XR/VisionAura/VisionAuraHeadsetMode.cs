using System.Collections.Generic;
using Astra.XR.Phone;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using XRUsages = UnityEngine.XR.CommonUsages;

namespace Astra.XR.VisionAura
{
    /// <summary>
    /// Vision Aura build only (ASTRA_VISION_AURA): while the iPhone headset is tracking, the scene is driven like a
    /// headset instead of a Mac window.
    /// <list type="bullet">
    /// <item>The head pose comes from the headset every frame (PhoneHeadPose re-enabled; desktop code disables it on Macs).
    /// The Vision Aura phone corrects each frame with the exact pose Unity rendered, so no other code may turn the camera:
    /// the desktop mouse rigs (<c>DesktopRig</c>, <c>HubDesktop</c>) are paused.</item>
    /// <item>The gaze rig and reticle are re-enabled; Mac-only welcome screens are skipped (they are invisible in the headset).</item>
    /// <item>Hands: the camera-derived "Vision Aura Hand Aim" becomes a visible pointer (ray + hand ring). Pinch or fist
    /// selects what it points at; the headset-button tap selects what you look at. Real hands are not available in VR:
    /// the runtime does not give camera images to Unity (they appear only in passthrough AR).</item>
    /// </list>
    /// Everything reverts when the headset session ends. Never scores or changes mission state.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class VisionAuraHeadsetMode : MonoBehaviour
    {
        private const string HandAimDevice = "Vision Aura Hand Aim";
        private const float LeaveAfterSeconds = 3f;
        private static readonly string[] MacOnlyBehaviours = { "DesktopRig", "HubDesktop" };

        private readonly List<InputDevice> devices = new List<InputDevice>();
        private readonly List<Behaviour> paused = new List<Behaviour>();
        private InputDevice hand;
        private float nextDeviceSearch;
        private bool headset;
        private bool applied;
        private float lastTracked = float.NegativeInfinity;
        private GazeInteractor gaze;
        private Transform aim;
        private LineRenderer ray;
        private LineRenderer ring;
        private readonly Vector3[] ringPoints = new Vector3[24];
        private int savedMsaa = -1;
        private bool savedHdr, savedDepth, savedOpaque, savedPipeline;
        private float savedShadowDistance;
        private int savedShadowResolution;
        private readonly List<UniversalAdditionalCameraData> postOff = new List<UniversalAdditionalCameraData>();
        private const float HandDistance = 0.4f;
        private const float GraceSeconds = 0.25f;
        private bool handPresent;
        private bool lastPinch;
        private float lastHandSeen = float.NegativeInfinity;
        private Transform parked;
        private const float PinchLookback = 0.12f;
        private const float AssistRadius = 0.025f;
        private bool pinchHeld;
        private Transform frozen;
        private Vector3 filteredPosition, filteredDirection = Vector3.forward, directionRate;
        private readonly List<(float time, Vector3 position, Vector3 direction)> history = new List<(float, Vector3, Vector3)>();
        private GazeReticleView reticleView;   // the aim ray starts at the head and passes through the palm
        private int savedQueuedFrames = -1;
        private readonly FrameTiming[] timings = new FrameTiming[1];
        private float statsSince;
        private int statsFrames;
        private double statsCpu, statsGpu;
        private int statsGpuSamples;

        public static bool HeadsetActive { get; private set; }
        public static VisionAuraHeadsetMode Instance { get; private set; }

        // ------------------------------------------------------------------ recenter
        private const string AutoRecenterKey = "astra.visionaura.autoRecenter";
        private const float AutoRecenterDelay = 0.6f;   // let the first head poses arrive
        private Vector3 seat;
        private bool seatKnown;
        private float autoRecenterAt = -1f;
        private bool wasHeadsetButton;

        /// <summary>Recenter automatically when the headset connects and on every scene load (remembered).</summary>
        public static bool AutoRecenter
        {
            get => PlayerPrefs.GetInt(AutoRecenterKey, 1) == 1;
            set { PlayerPrefs.SetInt(AutoRecenterKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// Turns the scene to face where the cadet is looking (yaw only, so the floor stays level) and puts the eye back
        /// at the seat. Moves the camera's parent; the headset's own tracking and the phone's correction are untouched.
        /// </summary>
        public void Recenter()
        {
            if (!HeadsetActive) return;   // the Mac window keeps its own mouse view
            var cam = Camera.main;
            if (cam == null || cam.transform.parent == null) return;
            var space = cam.transform.parent;
            if (!seatKnown) { seat = space.position; seatKnown = true; }
            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = cam.transform.up;   // looking straight down: use the top of the head
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return;
            var yaw = Vector3.SignedAngle(forward.normalized, Vector3.forward, Vector3.up);
            space.RotateAround(cam.transform.position, Vector3.up, yaw);
            space.position += seat - cam.transform.position;
            Debug.Log($"[ASTRA] Recentered (turned {yaw:0}°)");
        }

        private void Awake() => Instance = this;

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            seatKnown = false;
            applied = false;
            paused.Clear();
            gaze = null;
        }

        private void Update()
        {
            var tracked = HeadTracked();
            if (tracked) lastTracked = Time.unscaledTime;
            var active = XRSettings.isDeviceActive && (tracked || (headset && Time.unscaledTime - lastTracked < LeaveAfterSeconds));
            if (active != headset)
            {
                headset = active;
                HeadsetActive = active;
                if (active) { applied = false; EnterPerformanceTier(); }
                else { Restore(); LeavePerformanceTier(); }
            }
            if (!headset) return;
            if (!applied) Apply();
            UpdateRecenter();
            UpdateHand();
            LogFrameStats();
        }

        // ------------------------------------------------------------------ smooth head motion

        /// <summary>
        /// Streaming to the phone needs a steady 60 fps with the freshest pose: while the headset is on, let at most one
        /// frame queue and lighten the desktop render pipeline (MSAA 4x -> 2x). Restored when the headset leaves.
        /// </summary>
        private void EnterPerformanceTier()
        {
            savedQueuedFrames = QualitySettings.maxQueuedFrames;
            QualitySettings.maxQueuedFrames = 1;
            Application.targetFrameRate = 60;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                // Frames were taking 70-90 ms on the GPU (shared with the Agent's encoder): drop the costliest work so
                // each streamed frame is fresh. No ASTRA shader reads the depth/opaque copies.
                savedPipeline = true;
                savedMsaa = urp.msaaSampleCount;
                savedHdr = urp.supportsHDR;
                savedDepth = urp.supportsCameraDepthTexture;
                savedOpaque = urp.supportsCameraOpaqueTexture;
                savedShadowDistance = urp.shadowDistance;
                savedShadowResolution = urp.mainLightShadowmapResolution;
                if (urp.msaaSampleCount > 2) urp.msaaSampleCount = 2;
                urp.supportsHDR = false;
                urp.supportsCameraDepthTexture = false;
                urp.supportsCameraOpaqueTexture = false;
                urp.shadowDistance = Mathf.Min(urp.shadowDistance, 3f);
                urp.mainLightShadowmapResolution = Mathf.Min(urp.mainLightShadowmapResolution, 1024);
            }
            statsSince = Time.unscaledTime;
            statsFrames = 0;
            statsCpu = statsGpu = 0;
            statsGpuSamples = 0;
            Debug.Log($"[ASTRA] Headset mode: maxQueuedFrames=1, msaa={(GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset)?.msaaSampleCount}, 60 fps target");
        }

        private void LeavePerformanceTier()
        {
            if (savedQueuedFrames >= 0) QualitySettings.maxQueuedFrames = savedQueuedFrames;
            if (savedPipeline && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.msaaSampleCount = savedMsaa;
                urp.supportsHDR = savedHdr;
                urp.supportsCameraDepthTexture = savedDepth;
                urp.supportsCameraOpaqueTexture = savedOpaque;
                urp.shadowDistance = savedShadowDistance;
                urp.mainLightShadowmapResolution = savedShadowResolution;
            }
            foreach (var data in postOff)
                if (data != null) data.renderPostProcessing = true;
            postOff.Clear();
            savedPipeline = false;
            savedMsaa = savedQueuedFrames = -1;
        }

        /// <summary>The phone tier's trims (as PlatformQuality does on phones): no realtime probe, no dust, hard shadows.</summary>
        private void TrimScene()
        {
            // Post-processing (bloom, tonemapping, vignette, grading) is the largest per-eye cost; off in the headset.
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null && data.renderPostProcessing)
                {
                    data.renderPostProcessing = false;
                    postOff.Add(data);
                }
            }
            foreach (var probe in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
                probe.enabled = false;
            var dust = GameObject.Find("Microgravity_Dust");
            if (dust != null) dust.SetActive(false);
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.shadows == LightShadows.Soft) light.shadows = LightShadows.Hard;
        }

        /// <summary>Every 5 s: fps and CPU/GPU frame times in Player.log, to tell app load from streaming delay.</summary>
        private void LogFrameStats()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
            {
                statsCpu += timings[0].cpuFrameTime;
                if (timings[0].gpuFrameTime > 0) { statsGpu += timings[0].gpuFrameTime; statsGpuSamples++; }
            }
            statsFrames++;
            var elapsed = Time.unscaledTime - statsSince;
            if (elapsed < 5f) return;
            var gpu = statsGpuSamples > 0 ? (statsGpu / statsGpuSamples).ToString("0.0") : "n/a";
            Debug.Log($"[ASTRA] headset fps={statsFrames / elapsed:0.0} cpu={statsCpu / Mathf.Max(1, statsFrames):0.0}ms gpu={gpu}ms scene={SceneManager.GetActiveScene().name}");
            statsSince = Time.unscaledTime;
            statsFrames = 0;
            statsCpu = statsGpu = 0;
            statsGpuSamples = 0;
        }

        private static bool HeadTracked()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return head.isValid && head.TryGetFeatureValue(XRUsages.isTracked, out var t) && t;
        }

        private void Apply()
        {
            applied = true;
            TrimScene();
            foreach (var pose in FindObjectsByType<PhoneHeadPose>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                pose.SetMouseLook(false);
                pose.enabled = true;
            }
            foreach (var behaviour in FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var typeName = behaviour.GetType().Name;
                if (System.Array.IndexOf(MacOnlyBehaviours, typeName) >= 0 && behaviour.enabled)
                {
                    behaviour.enabled = false;
                    paused.Add(behaviour);
                }
                // The level briefing waits on a Mac-only welcome card; start it so the headset view is not stuck paused.
                if (typeName == "DesktopHud")
                    behaviour.SendMessage("StartMission", SendMessageOptions.DontRequireReceiver);
            }
            foreach (var view in FindObjectsByType<GazeReticleView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                view.gameObject.SetActive(true);
                var label = view.transform.Find("Hover Label");
                if (label != null) label.gameObject.SetActive(true);
            }
            gaze = FindAnyObjectByType<GazeInteractor>(FindObjectsInactive.Include);
            if (gaze != null)
            {
                gaze.SetMouseAndKeyboard(false);   // the Mac mouse must not steer or click in the headset
                gaze.SetMouseAim(false);
                // Hand-only selection in the headset: no look-to-select (dwell), no head-gaze clicks.
                gaze.UseGlobalDwellSetting(false);
                gaze.DwellSelectEnabled = false;
                gaze.LongPressRecenter = false;   // holding a pinch must never jump the view
                gaze.enabled = true;
            }
            reticleView = FindAnyObjectByType<GazeReticleView>(FindObjectsInactive.Include);
            handPresent = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EnsureVisuals();
            var cam = Camera.main;
            if (cam != null && cam.transform.parent != null) { seat = cam.transform.parent.position; seatKnown = true; }
            autoRecenterAt = AutoRecenter ? Time.unscaledTime + AutoRecenterDelay : -1f;
        }

        private void Restore()
        {
            if (gaze != null)
            {
                gaze.AimOverride = null;
                gaze.SetSimulatedPress(false);
            }
            foreach (var behaviour in paused)
                if (behaviour != null) behaviour.enabled = true;
            paused.Clear();
            // Back to the Mac window: the desktop mouse rigs own the camera again.
            foreach (var pose in FindObjectsByType<PhoneHeadPose>(FindObjectsSortMode.None))
                pose.enabled = false;
            if (gaze != null && FindAnyObjectByType<Camera>() != null && IsLevelScene())
            {
                gaze.enabled = false;
                foreach (var view in FindObjectsByType<GazeReticleView>(FindObjectsSortMode.None))
                    view.gameObject.SetActive(false);
            }
            else if (gaze != null)
            {
                gaze.SetMouseAndKeyboard(true);
                gaze.UseGlobalDwellSetting(true);
                gaze.LongPressRecenter = true;
                gaze.enabled = true;
                if (reticleView != null) reticleView.gameObject.SetActive(true);
            }
            SetVisuals(false);
            applied = false;
        }

        private static bool IsLevelScene()
        {
            var name = SceneManager.GetActiveScene().name;
            return name.Length > 1 && name[0] == 'L' && char.IsDigit(name[1]);
        }

        /// <summary>Auto-recenter once per scene/session, and a headset-button tap recenters on demand.</summary>
        private void UpdateRecenter()
        {
            if (autoRecenterAt > 0f && Time.unscaledTime >= autoRecenterAt && HeadTracked())
            {
                autoRecenterAt = -1f;
                Recenter();
            }
            var pressed = false;
            if (hand.isValid) hand.TryGetFeatureValue(XRUsages.menuButton, out pressed);   // short headset-button tap
            if (pressed && !wasHeadsetButton) Recenter();
            wasHeadsetButton = pressed;
        }

        // ------------------------------------------------------------------ hand pointer

        private void UpdateHand()
        {
            if (gaze == null) return;
            if (!hand.isValid && Time.unscaledTime >= nextDeviceSearch)
            {
                nextDeviceSearch = Time.unscaledTime + 1f;
                InputDevices.GetDevices(devices);
                foreach (var d in devices)
                    if (d.isValid && d.name == HandAimDevice) { hand = d; break; }
            }

            // Pinch is the only input. Hand tracking runs at camera rate and drops frames, so: smooth the aim, hold the
            // last pose (and pinch state) through dropouts under GraceSeconds, and never switch the pointer off/on.
            var fresh = false;
            var pinch = false;
            if (hand.isValid &&
                hand.TryGetFeatureValue(XRUsages.isTracked, out var t) && t &&
                hand.TryGetFeatureValue(XRUsages.devicePosition, out var position) &&
                hand.TryGetFeatureValue(XRUsages.deviceRotation, out var rotation) &&
                ToWorld(position, rotation, out var worldPosition, out var worldRotation))
            {
                fresh = true;
                hand.TryGetFeatureValue(XRUsages.triggerButton, out pinch);
                Filter(worldPosition, worldRotation * Vector3.forward, !handPresent);
                lastHandSeen = Time.unscaledTime;
                lastPinch = pinch;
            }
            var present = fresh || (handPresent && Time.unscaledTime - lastHandSeen < GraceSeconds);
            if (!fresh && present) pinch = lastPinch;   // a brief dropout must not release (or re-press) the pinch
            handPresent = present;

            // Pinch stabilisation: closing the fingers moves the hand, so the press uses where you pointed just before
            // it started, and the pointer holds still while the pinch is held.
            if (present && pinch && !pinchHeld)
            {
                pinchHeld = true;
                PoseAt(Time.unscaledTime - PinchLookback, out var p0, out var d0);
                frozen.position = p0;
                frozen.rotation = Quaternion.LookRotation(d0);
            }
            else if (!pinch || !present)
            {
                pinchHeld = false;
            }
            if (!pinchHeld)
            {
                aim.position = filteredPosition;
                aim.rotation = Quaternion.LookRotation(filteredDirection);
            }
            var pointer = pinchHeld ? frozen : aim;

            if (gaze.DwellSelectEnabled) gaze.DwellSelectEnabled = false;   // e.g. a level view turning dwell on
            gaze.AimOverride = present ? pointer : Parked();
            gaze.AssistRadius = AssistRadius;
            gaze.SetSimulatedPress(present && pinch);
            if (reticleView != null && reticleView.gameObject.activeSelf != present) reticleView.gameObject.SetActive(present);
            SetVisuals(present);
            if (!present) return;
            var select = pinch;

            // Draw at the hand (about 40 cm along the head-to-palm ray), never at the eyes: geometry a few centimetres
            // from the eyes is seen double in stereo.
            var handPoint = pointer.position + pointer.forward * HandDistance;
            var start = handPoint + pointer.forward * 0.03f;
            var end = gaze.HasHit && gaze.HitDistance > HandDistance + 0.05f ? gaze.HitPoint : pointer.position + pointer.forward * 2f;
            ray.SetPosition(0, start);
            ray.SetPosition(1, end);
            var color = gaze.Hovered != null ? new Color(0.25f, 0.61f, 1f, 1f) : new Color(1f, 1f, 1f, 0.7f);
            ray.startColor = new Color(color.r, color.g, color.b, 0.15f);
            ray.endColor = color;

            // A ring at the hand so you can see where it is tracked.
            var cam = Camera.main;
            var facing = cam != null ? Quaternion.LookRotation(handPoint - cam.transform.position) : pointer.rotation;
            for (var i = 0; i < ringPoints.Length; i++)
            {
                var a = i * Mathf.PI * 2f / ringPoints.Length;
                ringPoints[i] = handPoint + facing * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.02f;
            }
            ring.SetPositions(ringPoints);
            ring.startColor = ring.endColor = select ? new Color(0.19f, 0.82f, 0.35f, 1f) : color;
        }

        /// <summary>Hand aim is reported in the headset's tracking space, the same space as the head pose.</summary>
        private bool ToWorld(Vector3 position, Quaternion rotation, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            worldPosition = position;
            worldRotation = rotation;
            var cam = Camera.main;
            if (cam == null || aim == null) return false;
            var space = cam.transform.parent;
            worldPosition = space != null ? space.TransformPoint(position) : position;
            worldRotation = (space != null ? space.rotation : Quaternion.identity) * rotation;
            return true;
        }

        /// <summary>
        /// One Euro filter (Casiez et al.): strong smoothing while the hand is still, almost none while it moves, so the
        /// pointer is steady on a button yet follows quick motions without lag. Also keeps 0.3 s of history.
        /// </summary>
        private void Filter(Vector3 position, Vector3 direction, bool reset)
        {
            var dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            if (reset)
            {
                filteredPosition = position;
                filteredDirection = direction.normalized;
                directionRate = Vector3.zero;
                history.Clear();
            }
            else
            {
                const float minCutoff = 1.2f, beta = 0.9f, derivativeCutoff = 1.5f;
                var rawRate = (direction - filteredDirection) / dt;
                directionRate = Vector3.Lerp(directionRate, rawRate, Alpha(derivativeCutoff, dt));
                var cutoff = minCutoff + beta * directionRate.magnitude * 10f;
                var a = Alpha(cutoff, dt);
                filteredDirection = Vector3.Slerp(filteredDirection, direction.normalized, a).normalized;
                filteredPosition = Vector3.Lerp(filteredPosition, position, a);
            }
            history.Add((Time.unscaledTime, filteredPosition, filteredDirection));
            while (history.Count > 0 && Time.unscaledTime - history[0].time > 0.3f) history.RemoveAt(0);
        }

        private static float Alpha(float cutoff, float dt)
        {
            var tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / dt);
        }

        private void PoseAt(float time, out Vector3 position, out Vector3 direction)
        {
            position = filteredPosition;
            direction = filteredDirection;
            for (var i = history.Count - 1; i >= 0; i--)
            {
                position = history[i].position;
                direction = history[i].direction;
                if (history[i].time <= time) break;
            }
        }

        /// <summary>Where the pointer rests with no hand: far below the floor, pointing down, so nothing is hit.</summary>
        private Transform Parked()
        {
            if (parked == null)
            {
                parked = new GameObject("Pointer Parked").transform;
                parked.SetParent(transform, false);
                parked.position = new Vector3(0f, -1000f, 0f);
                parked.rotation = Quaternion.LookRotation(Vector3.down);
            }
            return parked;
        }

        private void EnsureVisuals()
        {
            if (aim != null) return;
            aim = new GameObject("Vision Aura Hand Aim").transform;
            aim.SetParent(transform, false);
            frozen = new GameObject("Vision Aura Hand Aim (pinch)").transform;
            frozen.SetParent(transform, false);
            Material material = null;
            var reticle = FindAnyObjectByType<GazeReticleView>(FindObjectsInactive.Include);
            if (reticle != null)
            {
                var renderer = reticle.GetComponentInChildren<MeshRenderer>(true);
                if (renderer != null) material = renderer.sharedMaterial;
            }
            if (material == null)
            {
                var shader = Shader.Find("ASTRA/ReticleOverlay") ?? Shader.Find("Sprites/Default");
                if (shader != null) material = new Material(shader);
            }
            ray = Line("Hand Ray", material, 2, false, 0.004f);
            ring = Line("Hand Ring", material, ringPoints.Length, true, 0.003f);
            SetVisuals(false);
        }

        private LineRenderer Line(string lineName, Material material, int points, bool loop, float width)
        {
            var line = new GameObject(lineName).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = points;
            line.loop = loop;
            line.startWidth = line.endWidth = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void SetVisuals(bool on)
        {
            if (ray != null) ray.enabled = on;
            if (ring != null) ring.enabled = on;
        }
    }
}

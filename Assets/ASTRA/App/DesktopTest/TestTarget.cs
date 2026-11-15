using System.Collections;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.XR.Input;
using Astra.XR.Interactables;
using TMPro;
using UnityEngine;

namespace Astra.DesktopTest
{
    /// <summary>
    /// TEMPORARY desktop/WebGL test interactable. Raises the contract event for its targetId when clicked.
    /// Replaced by XR & Interaction's ControlTarget / GrabbableTool / PlacementSocket; it never scores anything.
    /// Tools and sockets follow CONTRACT §3: click a tool = object_grabbed (it follows the view); click its
    /// matching socket = object_placed (socket id); click any other socket while holding = object_released.
    /// OWNER: Integration Lead (test tooling).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TestTarget : MonoBehaviour, IGazeTarget, IGazeLocatable, IGazeLabel
    {
        public enum Kind { Button, Toggle, Dial, Panel, Checklist, Lever, Tool, Socket }

        public static readonly Dictionary<string, TestTarget> Registry = new Dictionary<string, TestTarget>();
        public static TestTarget Held { get; private set; }

        [SerializeField] private string targetId;
        [SerializeField] private string eventName = EventNames.ControlActivated;
        [SerializeField] private Kind kind = Kind.Button;
        [Tooltip("Moving part (button cap, toggle lever, dial knob). Optional.")]
        [SerializeField] private Transform part;
        [SerializeField] private Renderer highlightRenderer;
        [SerializeField] private TMP_Text stateLabel;
        [Tooltip("Socket only: the tool targetId it accepts.")]
        [SerializeField] private string accepts;
        [Tooltip("Label shown under the gaze reticle (e.g. CABIN LIGHTS).")]
        [SerializeField] private string label;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _block;
        private Color _baseColor;
        private bool _on, _hover, _demo;
        private Quaternion _partRest;
        private Vector3 _partRestPos, _homePos;
        private Quaternion _homeRot;
        private Transform _homeParent;
        private string trigger;            // state that raises the mission event: ON/OFF (toggle, lever) or a dial position name
        private string[] positions;        // dial positions, cycled in order
        private bool decoy;                // decoys raise control_activated on every activation
        private int _posIndex;

        // ---- presentation-only animation state (never touches event logic)
        private static readonly Color DemoBlue = new Color(0.25f, 0.6f, 1f);
        private static readonly Color DemoGlow = new Color(0.1f, 0.3f, 0.8f);
        private static readonly Color GoodFlash = new Color(0.19f, 0.82f, 0.35f);
        private static readonly Color BadFlash = new Color(1f, 0.35f, 0.3f);
        private const float MaxDt = 0.05f;
        private float _curAngle, _angFrom, _angTo, _angT, _angDur = 0.18f;
        private bool _angActive, _angOvershoot;
        private float _tickT = -1f;
        private Vector3 _partRestScale = Vector3.one;
        private int _btnPhase;                       // 0 idle, 1 pressing in, 2 releasing
        private float _btnT, _btnFrom, _btnDepth;
        private bool _toolActive, _toolEnableCollider;
        private Vector3 _toolFromPos, _toolToPos;
        private Quaternion _toolFromRot, _toolToRot;
        private float _toolT, _toolDur;
        private float _hoverK, _demoK, _flashT, _flashDur;
        private Color _flashColor;
        private bool _visDirty;

        public string TargetId => targetId;
        public Kind TargetKind => kind;
        public string Accepts => accepts;

        // ---- phone gaze rig (P1's IGazeTarget): same behaviour as a click, reported as "gaze"
        public bool IsGazeEnabled { get { var c = GetComponent<Collider>(); return isActiveAndEnabled && c != null && c.enabled; } }
        public void SetGazeHover(bool on) => SetHover(on);
        public void ActivateFromGaze() => Activate(InputModality.Gaze);
        public float GazeLocateSeconds => kind == Kind.Panel ? 1f : 0f;   // CONTRACT §3: locating = 1 s look
        public string GazeLabel => label;

        public void Configure(string id, string evt, Kind k, Transform movingPart, Renderer highlight, TMP_Text state, string acceptsTool = null, string displayLabel = null, string triggerState = null, string[] dialPositions = null, bool isDecoy = false)
        {
            targetId = id; eventName = evt; kind = k; part = movingPart; highlightRenderer = highlight; stateLabel = state; accepts = acceptsTool; label = displayLabel;
            SetStateRules(triggerState, dialPositions, isDecoy);
        }

        /// <summary>Which state raises the mission event. Null trigger: ON for toggles/levers, every turn for dials.</summary>
        public void SetStateRules(string triggerState, string[] dialPositions, bool isDecoy)
        {
            trigger = triggerState; positions = dialPositions; decoy = isDecoy;
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (highlightRenderer != null) _baseColor = highlightRenderer.sharedMaterial.GetColor(BaseColor);
            if (part != null) { _partRest = part.localRotation; _partRestPos = part.localPosition; _partRestScale = part.localScale; }
            _homePos = transform.position; _homeRot = transform.rotation; _homeParent = transform.parent;
            UpdateStateLabel();
            ApplyDialAngle();
        }

        private void OnEnable() { if (!string.IsNullOrEmpty(targetId)) Registry[targetId] = this; }
        private void OnDisable()
        {
            FinishAnimations();
            if (Registry.TryGetValue(targetId ?? "", out var t) && t == this) Registry.Remove(targetId);
            if (Held == this) Held = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Registry.Clear(); Held = null; }

        public void SetHover(bool on)
        {
            if (on && !_hover && Astra.App.LevelAudio.Cues != null) Astra.App.LevelAudio.Play(Astra.App.LevelAudio.Cues.hover, 0.25f);
            _hover = on; _visDirty = true;
        }
        public void SetDemoHighlight(bool on) { _demo = on; _visDirty = true; }

        /// <summary>New attempt: tools go home, toggles/levers go back to OFF.</summary>
        public static void ResetAll()
        {
            Held = null;
            foreach (var t in Registry.Values)
            {
                t.FinishAnimations();                      // instant reset: no animation survives a phase change
                if (t.kind == Kind.Tool) t.GoHome(true);
                if (t.kind == Kind.Toggle || t.kind == Kind.Lever)
                {
                    t._on = false;
                    t._curAngle = 0f;
                    if (t.part != null) t.part.localRotation = t._partRest;
                    t.UpdateStateLabel();
                }
                if (t.kind == Kind.Dial) { t._posIndex = 0; t.ApplyDialAngle(); }
            }
        }

        public void Activate() => Activate(InputModality.Simulator);

        private void PlayCue()
        {
            var c = Astra.App.LevelAudio.Cues;
            if (c == null) return;
            var clip = kind switch
            {
                Kind.Toggle or Kind.Lever => c.toggle,
                Kind.Dial => c.dialTick,
                Kind.Tool => c.grab,
                Kind.Socket => Held != null ? c.place : null,
                Kind.Panel => c.select,
                _ => c.button,
            };
            Astra.App.LevelAudio.Play(clip, 0.8f);
        }

        /// <summary>Uses the control; <paramref name="modality"/> is reported with the event (simulator, gaze, ...).</summary>
        public void Activate(string modality)
        {
            PlayCue();
            switch (kind)
            {
                case Kind.Toggle:
                case Kind.Lever:
                    _on = !_on;
                    AnimatePart(_on ? (kind == Kind.Lever ? -90f : -70f) : 0f, 0.18f, true);
                    UpdateStateLabel();
                    if (!decoy && (_on ? "ON" : "OFF") != (string.IsNullOrEmpty(trigger) ? "ON" : trigger)) return;   // not the required state: no event
                    break;
                case Kind.Dial:
                    int n = positions != null && positions.Length > 0 ? positions.Length : 2;
                    _posIndex = (_posIndex + 1) % n;
                    AnimatePart(-45f + 45f * _posIndex, 0.2f, false);
                    if (!decoy && !string.IsNullOrEmpty(trigger) && positions != null && positions.Length > 0 && positions[_posIndex] != trigger) return;   // wrong position: no event
                    break;
                case Kind.Button:
                    if (part != null) { _btnPhase = 1; _btnT = 0f; _btnFrom = _btnDepth; }
                    break;
                case Kind.Tool:
                    if (Held == this) return;                       // already held
                    if (Held != null) Held.Drop();                  // swap: put the other one back
                    Held = this;
                    GetComponent<Collider>().enabled = false;      // never blocks the crosshair while held
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        transform.SetParent(cam.transform, true);
                        StartToolMove(new Vector3(0.12f, -0.12f, 0.35f), transform.localRotation, 0.25f, false);   // glide "in hand", lower right of view
                    }
                    InteractionEvents.Raise(EventNames.ObjectGrabbed, targetId, modality);
                    return;
                case Kind.Socket:
                    if (Held == null) return;                       // nothing to place
                    var tool = Held; Held = null;
                    if (tool.targetId == accepts)
                    {
                        tool.transform.SetParent(transform, true);
                        tool.GetComponent<Collider>().enabled = false;                 // off while it glides in; re-enabled on arrival
                        tool.StartToolMove(transform.InverseTransformPoint(transform.position - transform.forward * 0.03f), Quaternion.identity, 0.25f, true);
                        Flash(GoodFlash, 0.4f);
                        InteractionEvents.Raise(EventNames.ObjectPlaced, targetId, modality);   // socket id (CONTRACT §3)
                    }
                    else
                    {
                        tool.Drop();
                        InteractionEvents.Raise(EventNames.ObjectReleased, tool.targetId, modality);
                    }
                    return;
            }
            if (decoy) Flash(BadFlash, 0.3f); else Flash(GoodFlash, 0.4f);   // visual only
            InteractionEvents.Raise(eventName, targetId, modality);
        }

        private void Drop() { GoHome(); }

        private void GoHome(bool instant = false)
        {
            transform.SetParent(_homeParent, true);
            var col = GetComponent<Collider>();
            if (instant)
            {
                _toolActive = false;
                transform.SetPositionAndRotation(_homePos, _homeRot);
                col.enabled = true;
                return;
            }
            col.enabled = false;                                   // no blocking the crosshair mid-glide
            Vector3 lp = _homeParent != null ? _homeParent.InverseTransformPoint(_homePos) : _homePos;
            Quaternion lr = _homeParent != null ? Quaternion.Inverse(_homeParent.rotation) * _homeRot : _homeRot;
            StartToolMove(lp, lr, 0.35f, true);
        }

        // ------------------------------------------------------------------ animation (visual only, unscaled time)

        private static float EaseOutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }

        private static float EaseOutBack(float t, float s)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (s + 1f) * t * t * t + s * t * t;
        }

        private Quaternion PartRot(float a) => _partRest * (kind == Kind.Dial ? Quaternion.Euler(0f, 0f, a) : Quaternion.Euler(a, 0f, 0f));

        private void AnimatePart(float target, float dur, bool overshoot)
        {
            if (part == null) return;
            _angFrom = _curAngle;
            _angTo = _curAngle + Mathf.DeltaAngle(_curAngle, target);   // shortest way round
            _angT = 0f; _angDur = dur; _angOvershoot = overshoot; _angActive = true;
        }

        private void StartToolMove(Vector3 localPos, Quaternion localRot, float dur, bool enableColliderAtEnd)
        {
            _toolFromPos = transform.localPosition; _toolFromRot = transform.localRotation;
            _toolToPos = localPos; _toolToRot = localRot;
            _toolT = 0f; _toolDur = dur; _toolEnableCollider = enableColliderAtEnd; _toolActive = true;
        }

        private void Flash(Color c, float dur) { _flashColor = c; _flashDur = dur; _flashT = dur; _visDirty = true; }

        /// <summary>Jumps every running animation to its end state (used by ResetAll and OnDisable).</summary>
        private void FinishAnimations()
        {
            if (_angActive) { _angActive = false; _curAngle = _angTo; if (part != null) part.localRotation = PartRot(_curAngle); }
            if (_tickT >= 0f) { _tickT = -1f; if (part != null) part.localScale = _partRestScale; }
            if (_btnPhase != 0) { _btnPhase = 0; _btnDepth = 0f; if (part != null) part.localPosition = _partRestPos; }
            if (_toolActive)
            {
                _toolActive = false;
                transform.localPosition = _toolToPos; transform.localRotation = _toolToRot;
                if (_toolEnableCollider) { var c = GetComponent<Collider>(); if (c != null) c.enabled = true; }
            }
            if (_flashT > 0f) { _flashT = 0f; _visDirty = true; }
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxDt);
            if (part != null) { StepPart(dt); StepButton(dt); }
            if (_toolActive) StepTool(dt);
            StepVisual(dt);
        }

        private void StepPart(float dt)
        {
            if (_angActive)
            {
                _angT += dt / _angDur;
                float p = Mathf.Clamp01(_angT);
                float k = _angOvershoot ? EaseOutBack(p, 1.2f) : EaseOutCubic(p);   // <= ~5% past the target (under 5 degrees)
                _curAngle = _angFrom + (_angTo - _angFrom) * k;
                if (p >= 1f) { _curAngle = _angTo; _angActive = false; if (kind == Kind.Dial) _tickT = 0f; }
                part.localRotation = PartRot(_curAngle);
            }
            if (_tickT >= 0f)
            {
                _tickT += dt / 0.1f;
                if (_tickT >= 1f) { _tickT = -1f; part.localScale = _partRestScale; }
                else part.localScale = _partRestScale * (1f + 0.04f * Mathf.Sin(_tickT * Mathf.PI));   // 1.0 -> 1.04 -> 1.0
            }
        }

        private void StepButton(float dt)
        {
            if (_btnPhase == 0) return;
            const float depth = 0.004f;                              // 4 mm travel into the panel
            if (_btnPhase == 1)
            {
                _btnT += dt / 0.06f;
                _btnDepth = Mathf.Lerp(_btnFrom, depth, EaseOutCubic(_btnT));
                if (_btnT >= 1f) { _btnPhase = 2; _btnT = 0f; _btnFrom = depth; }
            }
            else
            {
                _btnT += dt / 0.12f;
                _btnDepth = Mathf.Lerp(_btnFrom, 0f, EaseOutCubic(_btnT));
                if (_btnT >= 1f) { _btnPhase = 0; _btnDepth = 0f; }
            }
            part.localPosition = _partRestPos + new Vector3(0f, 0f, _btnDepth);
        }

        private void StepTool(float dt)
        {
            _toolT += dt / _toolDur;
            float k = EaseOutCubic(_toolT);
            transform.localPosition = Vector3.LerpUnclamped(_toolFromPos, _toolToPos, k);
            transform.localRotation = Quaternion.Slerp(_toolFromRot, _toolToRot, k);
            if (_toolT >= 1f) FinishAnimations();
        }

        private void StepVisual(float dt)
        {
            if (highlightRenderer == null) return;
            float hT = _hover ? 1f : 0f, dT = _demo ? 1f : 0f;
            bool moving = false;
            if (_hoverK != hT) { _hoverK = Mathf.MoveTowards(_hoverK, hT, dt / 0.12f); moving = true; }
            if (_demoK != dT) { _demoK = Mathf.MoveTowards(_demoK, dT, dt / 0.2f); moving = true; }
            float fl = 0f;
            if (_flashT > 0f)
            {
                _flashT = Mathf.Max(0f, _flashT - dt);
                float f = _flashT / _flashDur;
                fl = f * f * Mathf.Clamp01((_flashDur - _flashT) / 0.04f);   // 40 ms rise, then fade out
                moving = true;
            }
            if (!moving && !_visDirty && _demoK <= 0f) return;
            _visDirty = moving || _demoK > 0f;                              // one last write once everything settled

            Color baseC = Color.Lerp(_baseColor, Color.white, 0.35f * _hoverK);
            baseC = Color.Lerp(baseC, DemoBlue, _demoK);
            Color em = Color.white * (0.12f * _hoverK);
            if (_demoK > 0f)
            {
                float breath = 0.35f + 0.65f * (0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * (Mathf.PI * 2f / 1.6f)));   // 0.625 Hz sine
                em += DemoGlow * (breath * _demoK);
            }
            if (fl > 0f)
            {
                baseC = Color.Lerp(baseC, _flashColor, 0.85f * fl);
                em += _flashColor * (0.9f * fl);
            }
            highlightRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, baseC);
            _block.SetColor(EmissionColor, em);
            highlightRenderer.SetPropertyBlock(_block);
        }

        // Matches the builder's dial labels (45 degree steps; label 0 is 45 degrees clockwise of up, so z = -45 + 45 * index).
        private void ApplyDialAngle()
        {
            if (kind != Kind.Dial || part == null) return;
            _angActive = false;
            _curAngle = -45f + 45f * _posIndex;
            part.localRotation = PartRot(_curAngle);
        }

        private void UpdateStateLabel()
        {
            if (stateLabel != null && (kind == Kind.Toggle || kind == Kind.Lever)) stateLabel.text = _on ? "ON" : "OFF";
        }
    }
}

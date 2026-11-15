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
            if (part != null) { _partRest = part.localRotation; _partRestPos = part.localPosition; }
            _homePos = transform.position; _homeRot = transform.rotation; _homeParent = transform.parent;
            UpdateStateLabel();
            ApplyDialAngle();
        }

        private void OnEnable() { if (!string.IsNullOrEmpty(targetId)) Registry[targetId] = this; }
        private void OnDisable()
        {
            if (Registry.TryGetValue(targetId ?? "", out var t) && t == this) Registry.Remove(targetId);
            if (Held == this) Held = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Registry.Clear(); Held = null; }

        public void SetHover(bool on)
        {
            if (on && !_hover && Astra.App.LevelAudio.Cues != null) Astra.App.LevelAudio.Play(Astra.App.LevelAudio.Cues.hover, 0.25f);
            _hover = on; Refresh();
        }
        public void SetDemoHighlight(bool on) { _demo = on; Refresh(); }

        /// <summary>New attempt: tools go home, toggles/levers go back to OFF.</summary>
        public static void ResetAll()
        {
            Held = null;
            foreach (var t in Registry.Values)
            {
                if (t.kind == Kind.Tool) t.GoHome();
                if (t.kind == Kind.Toggle || t.kind == Kind.Lever)
                {
                    t._on = false;
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
                    if (part != null) part.localRotation = _partRest * Quaternion.Euler(_on ? (kind == Kind.Lever ? -90f : -70f) : 0f, 0f, 0f);
                    UpdateStateLabel();
                    if (!decoy && (_on ? "ON" : "OFF") != (string.IsNullOrEmpty(trigger) ? "ON" : trigger)) return;   // not the required state: no event
                    break;
                case Kind.Dial:
                    int n = positions != null && positions.Length > 0 ? positions.Length : 2;
                    _posIndex = (_posIndex + 1) % n;
                    ApplyDialAngle();
                    if (!decoy && !string.IsNullOrEmpty(trigger) && positions != null && positions.Length > 0 && positions[_posIndex] != trigger) return;   // wrong position: no event
                    break;
                case Kind.Button:
                    if (part != null) StartCoroutine(Press());
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
                        transform.localPosition = new Vector3(0.12f, -0.12f, 0.35f);   // "in hand", lower right of view
                    }
                    InteractionEvents.Raise(EventNames.ObjectGrabbed, targetId, modality);
                    return;
                case Kind.Socket:
                    if (Held == null) return;                       // nothing to place
                    var tool = Held; Held = null;
                    if (tool.targetId == accepts)
                    {
                        tool.transform.SetParent(transform, true);
                        tool.transform.position = transform.position - transform.forward * 0.03f;
                        tool.transform.rotation = transform.rotation;
                        tool.GetComponent<Collider>().enabled = true;
                        InteractionEvents.Raise(EventNames.ObjectPlaced, targetId, modality);   // socket id (CONTRACT §3)
                    }
                    else
                    {
                        tool.Drop();
                        InteractionEvents.Raise(EventNames.ObjectReleased, tool.targetId, modality);
                    }
                    return;
            }
            InteractionEvents.Raise(eventName, targetId, modality);
        }

        private void Drop() { GoHome(); }

        private void GoHome()
        {
            transform.SetParent(_homeParent, true);
            transform.SetPositionAndRotation(_homePos, _homeRot);
            GetComponent<Collider>().enabled = true;
        }

        private IEnumerator Press()
        {
            part.localPosition = _partRestPos + new Vector3(0f, 0f, 0.004f);   // 4 mm travel into the panel
            yield return new WaitForSecondsRealtime(0.12f);
            part.localPosition = _partRestPos;
        }

        // Matches the builder's dial labels (45 degree steps; label 0 is 45 degrees clockwise of up, so z = -45 + 45 * index).
        private void ApplyDialAngle()
        {
            if (kind != Kind.Dial || part == null) return;
            part.localRotation = _partRest * Quaternion.Euler(0f, 0f, -45f + 45f * _posIndex);
        }

        private void UpdateStateLabel()
        {
            if (stateLabel != null && (kind == Kind.Toggle || kind == Kind.Lever)) stateLabel.text = _on ? "ON" : "OFF";
        }

        private void Refresh()
        {
            if (highlightRenderer == null) return;
            highlightRenderer.GetPropertyBlock(_block);
            Color c = _demo ? new Color(0.25f, 0.6f, 1f) : _hover ? Color.Lerp(_baseColor, Color.white, 0.35f) : _baseColor;
            _block.SetColor(BaseColor, c);
            _block.SetColor(EmissionColor, _demo ? new Color(0.1f, 0.3f, 0.8f) : Color.black);
            highlightRenderer.SetPropertyBlock(_block);
        }
    }
}

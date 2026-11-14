using Astra.App;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Core.Simulation;
using Astra.UI;
using TMPro;
using UnityEngine;

namespace Astra.Levels
{
    /// <summary>
    /// The power-fault drama shared by L4 Emergency Response and L7 Integrated Mission (OWNER: Core Logic).
    /// The level factory attaches the subclass (<c>L4EmergencyView</c> / <c>L7FinaleView</c>) and fills
    /// <c>level</c>, <c>cabinLights</c> and one Renderer per layout indicator (caution_light → cautionLight, …).
    /// <para>From the fault (start of Practice and Qualification) until the cross-tie restores power:
    /// the CAUTION alarm is on, the cabin lights dim to 35 %, BUS B shows FAULT, then ISOLATED.
    /// The CAUTION light flashes at 1.5 Hz until ACK, then stays on steady. Every light has a text label,
    /// so colour is never the only cue.</para>
    /// It only listens: it never calls the runner or changes a score (CONTRACT §8). The logic is Core's
    /// <see cref="PowerPanelModel"/>, which only reacts to Correct outcomes, so a wrong bus leaves the fault in place.
    /// </summary>
    public abstract class PowerFaultLevelView : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private Light[] cabinLights = new Light[0];
        [SerializeField] private Renderer cautionLight;
        [SerializeField] private Renderer busALight;
        [SerializeField] private Renderer busBLight;
        [SerializeField] private Renderer statusLight;
        [SerializeField, Range(0.1f, 1f)] private float faultLightScale = 0.35f;
        [SerializeField, Range(0f, 3f)] private float cautionFlashesPerSecond = 1.5f;
        [SerializeField] private float lightFadeSeconds = 0.8f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly Color Off = new Color(0.12f, 0.14f, 0.12f);
        private static readonly Color Green = new Color(0.19f, 0.82f, 0.35f);   // #30D158
        private static readonly Color Amber = new Color(1f, 0.70f, 0.25f);     // #FFB340
        private static readonly Color Red = new Color(1f, 0.41f, 0.38f);       // #FF6961

        private PowerPanelModel _model;
        private AlarmIndicator _alarm;
        private MaterialPropertyBlock _block;
        private TMP_Text _busBLabel, _statusLabel;
        private float[] _baseIntensity = new float[0];
        private Color _baseAmbient;
        private float _lightLevel = 1f, _lightTarget = 1f;
        private bool _flashing;

        /// <summary>The mission's isolate control (the BUS B dial or selector).</summary>
        protected abstract string IsolateTargetId { get; }

        /// <summary>The mission's cross-tie control.</summary>
        protected abstract string CrossTieTargetId { get; }

        /// <summary>The panel state, for tests and other level scripts. Null before Awake.</summary>
        public PowerPanelModel Model => _model;

        /// <summary>True while the cabin is dimmed and the alarm is on (fault until power is restored).</summary>
        public bool FaultActive => _model != null &&
                                   (_model.State == PowerBusState.Faulted || _model.State == PowerBusState.Isolated);

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _model = new PowerPanelModel(new PowerBusSimulation(IsolateTargetId, CrossTieTargetId), PowerPanelModel.DefaultAckTargetId);
            _busBLabel = LabelOf(busBLight);
            _statusLabel = LabelOf(statusLight);
        }

        private void OnEnable()
        {
            if (level == null) { Debug.LogError($"[{GetType().Name}] LevelController is not assigned.", this); enabled = false; return; }
            _model.Changed += Render;
            level.PhaseChanged += _model.OnPhaseChanged;
            level.EventRecorded += _model.OnEventRecorded;
        }

        private void OnDisable()
        {
            if (level == null || _model == null) return;
            _model.Changed -= Render;
            level.PhaseChanged -= _model.OnPhaseChanged;
            level.EventRecorded -= _model.OnEventRecorded;
        }

        private void Start()
        {
            _alarm = FindAnyObjectByType<AlarmIndicator>();
            _baseAmbient = RenderSettings.ambientLight;
            _baseIntensity = new float[cabinLights != null ? cabinLights.Length : 0];
            for (int i = 0; i < _baseIntensity.Length; i++)
                _baseIntensity[i] = cabinLights[i] != null ? cabinLights[i].intensity : 0f;
            Render();
        }

        private void Update()
        {
            if (!Mathf.Approximately(_lightLevel, _lightTarget))
            {
                float step = lightFadeSeconds > 0f ? Time.unscaledDeltaTime / lightFadeSeconds : 1f;
                _lightLevel = Mathf.MoveTowards(_lightLevel, _lightTarget, step);
                ApplyCabinLights();
            }
            if (_flashing && cautionFlashesPerSecond > 0f)
            {
                bool on = Mathf.Repeat(Time.unscaledTime * cautionFlashesPerSecond, 1f) < 0.5f;
                SetLight(cautionLight, on ? Amber : Off);
            }
        }

        private void Render()
        {
            var state = _model.State;
            bool fault = FaultActive;

            _lightTarget = fault ? faultLightScale : 1f;

            _flashing = state == PowerBusState.Faulted && !_model.Acknowledged;
            if (!_flashing) SetLight(cautionLight, fault ? Amber : Off);

            SetLight(busALight, Green);
            switch (state)
            {
                case PowerBusState.Faulted: SetLight(busBLight, Red); SetLabel(_busBLabel, "FAULT"); break;
                case PowerBusState.Isolated: SetLight(busBLight, Amber); SetLabel(_busBLabel, "ISOLATED"); break;
                case PowerBusState.Restored: SetLight(busBLight, Green); SetLabel(_busBLabel, "CROSS-TIED"); break;
                default: SetLight(busBLight, Green); SetLabel(_busBLabel, "NORMAL"); break;
            }

            SetLight(statusLight, state == PowerBusState.Faulted ? Red : state == PowerBusState.Isolated ? Amber : Green);
            SetLabel(_statusLabel, "STATUS: " + _model.StatusLabel);

            if (_alarm != null)
                _alarm.SetAlarm(fault, fault ? Caption(state, _model.Acknowledged) : string.Empty);
        }

        private static string Caption(PowerBusState state, bool acknowledged)
        {
            if (state == PowerBusState.Isolated) return "CAUTION — POWER: BUS B ISOLATED";
            return acknowledged ? "CAUTION — POWER: BUS B FAULT (ACK)" : "CAUTION — POWER: BUS B FAULT";
        }

        private void ApplyCabinLights()
        {
            for (int i = 0; i < _baseIntensity.Length; i++)
                if (cabinLights[i] != null) cabinLights[i].intensity = _baseIntensity[i] * _lightLevel;
            RenderSettings.ambientLight = _baseAmbient * _lightLevel;
        }

        private void SetLight(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, c);
            _block.SetColor(EmissionColor, c == Off ? Color.black : c * 1.6f);
            r.SetPropertyBlock(_block);
        }

        private static void SetLabel(TMP_Text label, string text)
        {
            if (label != null && label.text != text) label.text = text;
        }

        /// <summary>The factory puts an indicator's label next to it as "&lt;id&gt;_Label" under the same panel.</summary>
        private static TMP_Text LabelOf(Renderer indicator)
        {
            if (indicator == null || indicator.transform.parent == null) return null;
            var t = indicator.transform.parent.Find(indicator.gameObject.name + "_Label");
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }
    }
}

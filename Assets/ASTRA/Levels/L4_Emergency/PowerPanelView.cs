using Astra.App;
using Astra.Core.Simulation;
using Astra.UI;
using UnityEngine;
using UnityEngine.Events;

namespace Astra.Levels
{
    /// <summary>
    /// The power panel in L4 Emergency Response and L7 Integrated Mission (OWNER: Core Logic).
    /// Listens to the LevelController and shows PowerPanelModel's state: which lights/labels are visible,
    /// the STATUS text, and the caution alarm. It makes no decisions itself and never calls the runner
    /// (CONTRACT.md §8.6). Every state has a text label as well as a light, so colour is never the only cue.
    /// </summary>
    public class PowerPanelView : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private LevelController levelController;
        [SerializeField] private AlarmIndicator alarm;

        [Header("Control target ids (must match the level's mission file)")]
        [Tooltip("L7: power_bus_b_selector. L4: power_bus_b_dial.")]
        [SerializeField] private string isolateTargetId = PowerBusSimulation.DefaultIsolateTargetId;
        [Tooltip("L7: crosstie_breaker. L4: crosstie_lever.")]
        [SerializeField] private string crossTieTargetId = PowerBusSimulation.DefaultCrossTieTargetId;
        [SerializeField] private string ackTargetId = PowerPanelModel.DefaultAckTargetId;

        [Header("Alarm")]
        [SerializeField] private string cautionCaption = "CAUTION — POWER: BUS B FAULT";

        [Header("Objects shown only in that state (lights, labels)")]
        [SerializeField] private GameObject[] showWhenNominal = new GameObject[0];
        [SerializeField] private GameObject[] showWhenFaulted = new GameObject[0];
        [SerializeField] private GameObject[] showWhenIsolated = new GameObject[0];
        [SerializeField] private GameObject[] showWhenRestored = new GameObject[0];

        [Header("STATUS display text (NOMINAL, FAULT, ISOLATED, RESTORED)")]
        [SerializeField] private UnityEvent<string> onStatusText = new UnityEvent<string>();

        private PowerPanelModel _model;

        /// <summary>The panel state, for other level scripts to read. Null if the component is misconfigured.</summary>
        public PowerPanelModel Model => _model;

        private void Awake()
        {
            if (levelController == null)
                Debug.LogError($"[PowerPanelView] '{name}': 'Level Controller' is not assigned; the panel will not react.", this);
            if (alarm == null)
                Debug.LogError($"[PowerPanelView] '{name}': 'Alarm' is not assigned; the caution will have no light or caption.", this);

            try
            {
                _model = new PowerPanelModel(new PowerBusSimulation(isolateTargetId, crossTieTargetId), ackTargetId);
            }
            catch (System.ArgumentException ex)
            {
                Debug.LogError($"[PowerPanelView] '{name}': invalid control ids ({ex.Message}); panel disabled.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_model == null)
                return;
            _model.Changed += Render;
            if (levelController != null)
            {
                levelController.PhaseChanged += _model.OnPhaseChanged;
                levelController.EventRecorded += _model.OnEventRecorded;
            }
            Render();
        }

        private void OnDisable()
        {
            if (_model == null)
                return;
            _model.Changed -= Render;
            if (levelController != null)
            {
                levelController.PhaseChanged -= _model.OnPhaseChanged;
                levelController.EventRecorded -= _model.OnEventRecorded;
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(isolateTargetId) || string.IsNullOrEmpty(crossTieTargetId) || string.IsNullOrEmpty(ackTargetId))
                Debug.LogWarning($"[PowerPanelView] '{name}': every control target id must be filled in.", this);
            else if (isolateTargetId == crossTieTargetId)
                Debug.LogWarning($"[PowerPanelView] '{name}': isolate and cross-tie must be different controls.", this);
        }

        private void Render()
        {
            var state = _model.State;
            SetActive(showWhenNominal, state == PowerBusState.Nominal);
            SetActive(showWhenFaulted, state == PowerBusState.Faulted);
            SetActive(showWhenIsolated, state == PowerBusState.Isolated);
            SetActive(showWhenRestored, state == PowerBusState.Restored);

            onStatusText.Invoke(_model.StatusLabel);

            if (alarm != null)
                alarm.SetAlarm(_model.AlarmOn, _model.AlarmOn ? cautionCaption : string.Empty);
        }

        private static void SetActive(GameObject[] objects, bool active)
        {
            if (objects == null)
                return;
            foreach (var go in objects)
            {
                if (go != null && go.activeSelf != active)
                    go.SetActive(active);
            }
        }
    }
}

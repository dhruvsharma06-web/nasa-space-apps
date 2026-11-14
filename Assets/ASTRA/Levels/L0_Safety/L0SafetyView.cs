using Astra.App;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.XR.Phone;
using UnityEngine;

/// <summary>
/// L0-only feedback, attached automatically by the level factory: the STATUS light (indicator <c>ready_light</c>) turns
/// green once READY is tapped, and dwell-to-select is on by default in L0 (looking 1.5 s selects) so first-time cadets
/// can finish without mastering the tap. Reacts only to Correct outcomes; never drives the runner (CONTRACT §8).
/// OWNER: XR & Interaction (L0).
/// </summary>
public class L0SafetyView : MonoBehaviour
{
    [SerializeField] private LevelController level;
    [SerializeField] private Renderer readyLight;
    [SerializeField] private bool dwellSelectInL0 = true;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock _block;

    private void Awake() => _block = new MaterialPropertyBlock();

    private void Start()
    {
        if (!dwellSelectInL0) return;
        var gaze = FindAnyObjectByType<GazeInteractor>();
        if (gaze == null) return;
        gaze.UseGlobalDwellSetting(false);   // L0 only; the player's Hub setting applies everywhere else
        gaze.DwellSelectEnabled = true;
    }

    private void OnEnable()
    {
        if (level == null) { Debug.LogError("[L0SafetyView] LevelController is not assigned.", this); enabled = false; return; }
        level.PhaseChanged += OnPhase;
        level.EventRecorded += OnEvent;
        SetReady(false);
    }

    private void OnDisable()
    {
        if (level == null) return;
        level.PhaseChanged -= OnPhase;
        level.EventRecorded -= OnEvent;
    }

    private void OnPhase(ModulePhase phase)
    {
        if (phase == ModulePhase.Briefing || phase == ModulePhase.Practice || phase == ModulePhase.Qualification)
            SetReady(false);
    }

    private void OnEvent(SessionEvent e, StepFeedback fb)
    {
        if (fb.Outcome == StepOutcome.Correct && fb.StepId == "press_ready") SetReady(true);
    }

    private void SetReady(bool on)
    {
        if (readyLight == null || _block == null) return;
        readyLight.GetPropertyBlock(_block);
        _block.SetColor(BaseColor, on ? new Color(0.19f, 0.82f, 0.35f) : new Color(0.12f, 0.14f, 0.12f));
        _block.SetColor(EmissionColor, on ? new Color(0.2f, 1.4f, 0.4f) : Color.black);
        readyLight.SetPropertyBlock(_block);
    }
}

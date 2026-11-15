using Astra.App;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using UnityEngine;

/// <summary>
/// L6-only visual feedback: cabin lights dim at reset, brighten as the procedure progresses;
/// the optional armSafe indicator turns amber while in progress and green at the final step.
/// Reacts only to Correct outcomes recorded by the LevelController; never drives the runner (CONTRACT §8).
/// The indicator is filled by the level factory only if the layout declares an indicator id "arm_safe".
/// </summary>
public class L6RoboticsView : MonoBehaviour
{
    [SerializeField] private LevelController level;
    [SerializeField] private Light[] cabinLights;
    [SerializeField] private Renderer armSafe;
    [SerializeField] private float offIntensity = 0.5f, progressIntensity = 1.0f, doneIntensity = 1.6f;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock _block;

    private enum State { Off, InProgress, Done }

    private void Awake() => _block = new MaterialPropertyBlock();

    private void OnEnable()
    {
        if (level == null) { Debug.LogError("[L6RoboticsView] LevelController is not assigned.", this); enabled = false; return; }
        level.PhaseChanged += OnPhase;
        level.EventRecorded += OnEvent;
    }

    private void OnDisable()
    {
        if (level == null) return;
        level.PhaseChanged -= OnPhase;
        level.EventRecorded -= OnEvent;
    }

    private void OnPhase(ModulePhase phase)
    {
        if (phase == ModulePhase.Practice || phase == ModulePhase.Qualification || phase == ModulePhase.Briefing)
            Apply(State.Off);
    }

    private void OnEvent(SessionEvent e, StepFeedback fb)
    {
        if (fb.Outcome != StepOutcome.Correct) return;
        if (fb.StepId == "report") Apply(State.Done);
        else if (fb.StepId == "arm_power") Apply(State.InProgress);
    }

    private void Apply(State state)
    {
        SetLights(state == State.Done ? doneIntensity : state == State.InProgress ? progressIntensity : offIntensity);
        SetIndicator(state);
    }

    private void SetLights(float intensity)
    {
        if (cabinLights == null) return;
        foreach (var l in cabinLights) if (l != null) l.intensity = intensity;
    }

    private void SetIndicator(State state)
    {
        if (armSafe == null) return;
        Color baseCol = new Color(0.12f, 0.14f, 0.12f), emis = Color.black;
        if (state == State.InProgress) { baseCol = new Color(0.9f, 0.6f, 0.1f); emis = new Color(1.4f, 0.8f, 0.1f); }
        else if (state == State.Done) { baseCol = new Color(0.19f, 0.82f, 0.35f); emis = new Color(0.2f, 1.4f, 0.4f); }
        armSafe.GetPropertyBlock(_block);
        _block.SetColor(BaseColor, baseCol);
        _block.SetColor(EmissionColor, emis);
        armSafe.SetPropertyBlock(_block);
    }
}

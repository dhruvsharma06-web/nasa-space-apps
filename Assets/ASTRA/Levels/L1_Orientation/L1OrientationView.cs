using Astra.App;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using UnityEngine;

/// <summary>
/// L1-only feedback, attached automatically by the level factory: each panel's FOUND light turns green when the cadet
/// has located that panel (a 1 s gaze dwell). Reacts only to Correct outcomes; never drives the runner (CONTRACT §8).
/// OWNER: XR & Interaction (L1).
/// </summary>
public class L1OrientationView : MonoBehaviour
{
    [SerializeField] private LevelController level;
    [SerializeField] private Renderer commsFoundLight;
    [SerializeField] private Renderer powerFoundLight;
    [SerializeField] private Renderer lifeSupportFoundLight;
    [SerializeField] private Renderer stowageFoundLight;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock _block;

    private void Awake() => _block = new MaterialPropertyBlock();

    private void OnEnable()
    {
        if (level == null) { Debug.LogError("[L1OrientationView] LevelController is not assigned.", this); enabled = false; return; }
        level.PhaseChanged += OnPhase;
        level.EventRecorded += OnEvent;
        ResetAll();
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
            ResetAll();
    }

    private void OnEvent(SessionEvent e, StepFeedback fb)
    {
        if (fb.Outcome != StepOutcome.Correct) return;
        switch (fb.StepId)
        {
            case "locate_comms": Set(commsFoundLight, true); break;
            case "locate_power": Set(powerFoundLight, true); break;
            case "locate_life_support": Set(lifeSupportFoundLight, true); break;
            case "locate_stowage": Set(stowageFoundLight, true); break;
        }
    }

    private void ResetAll()
    {
        Set(commsFoundLight, false);
        Set(powerFoundLight, false);
        Set(lifeSupportFoundLight, false);
        Set(stowageFoundLight, false);
    }

    private void Set(Renderer light, bool on)
    {
        if (light == null || _block == null) return;
        light.GetPropertyBlock(_block);
        _block.SetColor(BaseColor, on ? new Color(0.19f, 0.82f, 0.35f) : new Color(0.12f, 0.14f, 0.12f));
        _block.SetColor(EmissionColor, on ? new Color(0.2f, 1.4f, 0.4f) : Color.black);
        light.SetPropertyBlock(_block);
    }
}

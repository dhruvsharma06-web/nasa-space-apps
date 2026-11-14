using Astra.App;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using UnityEngine;

/// <summary>
/// L3-only visual feedback: CABIN LIGHTS brighten the cabin; COMMS CHECK turns the READY light green.
/// Reacts only to Correct outcomes recorded by the LevelController; never drives the runner (CONTRACT §8).
/// OWNER: Content & AI Coach (L3).
/// </summary>
public class L3CabinView : MonoBehaviour
{
    [SerializeField] private LevelController level;
    [SerializeField] private Light[] cabinLights;
    [SerializeField] private Renderer readyLight;
    [SerializeField] private float offIntensity = 0.15f, onIntensity = 1.6f;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock _block;

    private void Awake() => _block = new MaterialPropertyBlock();

    private void OnEnable()
    {
        if (level == null) { Debug.LogError("[L3CabinView] LevelController is not assigned.", this); enabled = false; return; }
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
        {
            SetCabinLights(false);
            SetReady(false);
        }
    }

    private void OnEvent(SessionEvent e, StepFeedback fb)
    {
        if (fb.Outcome != StepOutcome.Correct) return;
        if (fb.StepId == "lights_on") SetCabinLights(true);
        if (fb.StepId == "comms_check") SetReady(true);
    }

    private void SetCabinLights(bool on)
    {
        if (cabinLights == null) return;
        foreach (var l in cabinLights) if (l != null) l.intensity = on ? onIntensity : offIntensity;
        RenderSettings.ambientLight = on ? new Color(0.42f, 0.44f, 0.48f) : new Color(0.16f, 0.18f, 0.22f);
    }

    private void SetReady(bool on)
    {
        if (readyLight == null) return;
        readyLight.GetPropertyBlock(_block);
        _block.SetColor(BaseColor, on ? new Color(0.19f, 0.82f, 0.35f) : new Color(0.12f, 0.14f, 0.12f));
        _block.SetColor(EmissionColor, on ? new Color(0.2f, 1.4f, 0.4f) : Color.black);
        readyLight.SetPropertyBlock(_block);
    }
}

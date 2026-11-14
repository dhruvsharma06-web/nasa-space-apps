using System;
using System.Collections;
using Astra.Core.Mission;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.UI
{
    /// <summary>Displays the current module phase with a short slide/fade (≤ 300 ms; the world never moves).</summary>
    public class PhaseBanner : MonoBehaviour
    {
        public const float TransitionSeconds = 0.28f;

        [SerializeField] private Text phaseLabel;
        [SerializeField, Range(0f, 60f)] private float slidePixels = 24f;
        private Coroutine transition;
        private Vector2 labelRest;
        private bool restKnown;

        public void Configure(Text label)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            phaseLabel = label;
            phaseLabel.supportRichText = false;
        }

        private void Awake()
        {
            if (!ValidateLabel())
            {
                enabled = false;
                return;
            }
            phaseLabel.supportRichText = false;
        }

        public virtual void Show(ModulePhase phase)
        {
            if (!ValidateLabel()) return;
            switch (phase)
            {
                case ModulePhase.Briefing: phaseLabel.text = "Briefing"; break;
                case ModulePhase.Demonstration: phaseLabel.text = "Demonstration"; break;
                case ModulePhase.Practice: phaseLabel.text = "Practice"; break;
                case ModulePhase.Qualification: phaseLabel.text = "Qualification"; break;
                case ModulePhase.Debrief: phaseLabel.text = "Debrief"; break;
                default:
                    phaseLabel.text = string.Empty;
                    Debug.LogError($"[PhaseBanner] Invalid module phase: {phase}.", this);
                    return;
            }
            phaseLabel.color = phase == ModulePhase.Qualification ? Theme.WarningInk : phase == ModulePhase.Debrief ? Theme.Ink : Theme.AccentInk;
            if (Application.isPlaying && isActiveAndEnabled)
            {
                if (transition != null) StopCoroutine(transition);
                transition = StartCoroutine(SlideIn());
            }
        }

        private IEnumerator SlideIn()
        {
            var rect = phaseLabel.rectTransform;
            if (!restKnown) { labelRest = rect.anchoredPosition; restKnown = true; }
            var color = phaseLabel.color;
            for (var t = 0f; t < TransitionSeconds; t += Time.unscaledDeltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / TransitionSeconds);
                rect.anchoredPosition = labelRest + new Vector2(0f, -slidePixels * (1f - k));
                phaseLabel.color = new Color(color.r, color.g, color.b, k);
                yield return null;
            }
            rect.anchoredPosition = labelRest;
            phaseLabel.color = color;
            transition = null;
        }

        private void OnDisable()
        {
            if (transition == null || phaseLabel == null) return;
            StopCoroutine(transition);
            transition = null;
            if (restKnown) phaseLabel.rectTransform.anchoredPosition = labelRest;
            var c = phaseLabel.color;
            phaseLabel.color = new Color(c.r, c.g, c.b, 1f);
        }

        private bool ValidateLabel()
        {
            if (phaseLabel != null) return true;
            Debug.LogError("[PhaseBanner] Missing phaseLabel.", this);
            return false;
        }
    }
}

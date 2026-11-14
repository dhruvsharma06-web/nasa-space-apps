using System;
using Astra.Core.Mission;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.UI
{
    /// <summary>Displays the current module phase.</summary>
    public class PhaseBanner : MonoBehaviour
    {
        [SerializeField] private Text phaseLabel;

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
                    break;
            }
        }

        private bool ValidateLabel()
        {
            if (phaseLabel != null) return true;
            Debug.LogError("[PhaseBanner] Missing phaseLabel.", this);
            return false;
        }
    }
}

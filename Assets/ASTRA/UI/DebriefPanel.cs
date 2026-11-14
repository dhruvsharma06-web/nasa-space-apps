using System;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Astra.UI
{
    /// <summary>
    /// Pass/retry outcome + actionable feedback (FR-AS-04).
    /// OWNER: XR & Interaction. Signatures fixed by CONTRACT.md §7.
    /// </summary>
    public class DebriefPanel : MonoBehaviour
    {
        [SerializeField] private Text outcomeLabel;
        [SerializeField] private Text detailsLabel;
        [SerializeField] private Text coachHeading;
        [SerializeField] private Text coachLabel;
        [SerializeField] private UnityEvent retry = new UnityEvent();
        [SerializeField] private UnityEvent backToHub = new UnityEvent();

        public UnityEvent onRetry => retry;
        public UnityEvent onBackToHub => backToHub;

        public void Configure(Text outcome, Text details, Text coachTitle, Text advice)
        {
            if (outcome == null || details == null || coachTitle == null || advice == null)
                throw new ArgumentException("All four debrief text references are required.");
            if (outcome == details || outcome == coachTitle || outcome == advice ||
                details == coachTitle || details == advice || coachTitle == advice)
                throw new ArgumentException("Debrief text references must be distinct.");

            outcomeLabel = outcome;
            detailsLabel = details;
            coachHeading = coachTitle;
            coachLabel = advice;
        }

        public virtual void Show(SessionResult result, MissionDefinition mission)
        {
            if (!ValidateReferences())
                return;
            PrepareText();
            outcomeLabel.enabled = true;
            detailsLabel.enabled = true;
            ClearCoach();
            if (result == null || mission == null ||
                !string.Equals(result.missionId, mission.missionId, StringComparison.Ordinal))
            {
                outcomeLabel.text = "Debrief unavailable";
                detailsLabel.text = string.Empty;
                Debug.LogError($"[DebriefPanel] {name}: result and mission are required and must have matching missionId values.", this);
                return;
            }

            outcomeLabel.text = result.passed ? "PASS" : "RETRY";
            detailsLabel.text = DebriefTextFormatter.Format(result, mission);
        }

        /// <param name="isAi">True when the text came from the AI coach; the panel must label it "AI coach (advisory)".</param>
        public virtual void ShowCoachText(string text, bool isAi)
        {
            if (!ValidateReferences())
                return;
            PrepareText();
            if (string.IsNullOrWhiteSpace(text))
            {
                ClearCoach();
                return;
            }
            coachHeading.text = isAi ? "AI coach (advisory)" : "Coach";
            coachLabel.text = text;
            coachHeading.enabled = true;
            coachLabel.enabled = true;
        }

        public void RequestRetry() => retry.Invoke();
        public void RequestBackToHub() => backToHub.Invoke();

        protected virtual void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }
            PrepareText();
            outcomeLabel.text = string.Empty;
            detailsLabel.text = string.Empty;
            ClearCoach();
        }

        private void PrepareText()
        {
            outcomeLabel.supportRichText = false;
            detailsLabel.supportRichText = false;
            coachHeading.supportRichText = false;
            coachLabel.supportRichText = false;
        }

        private void ClearCoach()
        {
            coachHeading.text = string.Empty;
            coachLabel.text = string.Empty;
            coachHeading.enabled = false;
            coachLabel.enabled = false;
        }

        private bool ValidateReferences()
        {
            if (outcomeLabel == null)
                Debug.LogError($"[DebriefPanel] {name}: missing outcomeLabel reference.", this);
            if (detailsLabel == null)
                Debug.LogError($"[DebriefPanel] {name}: missing detailsLabel reference.", this);
            if (coachHeading == null)
                Debug.LogError($"[DebriefPanel] {name}: missing coachHeading reference.", this);
            if (coachLabel == null)
                Debug.LogError($"[DebriefPanel] {name}: missing coachLabel reference.", this);
            if (outcomeLabel == null || detailsLabel == null || coachHeading == null || coachLabel == null)
                return false;
            if (outcomeLabel == detailsLabel || outcomeLabel == coachHeading || outcomeLabel == coachLabel ||
                detailsLabel == coachHeading || detailsLabel == coachLabel || coachHeading == coachLabel)
            {
                Debug.LogError($"[DebriefPanel] {name}: text references must be distinct.", this);
                return false;
            }
            return true;
        }
    }
}

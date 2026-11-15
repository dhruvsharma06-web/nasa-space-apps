using System;
using System.Collections;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.UI.Progress;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Astra.UI
{
    /// <summary>
    /// Pass/retry outcome + actionable feedback (FR-AS-04), with the score reveal (TASKS S9):
    /// score counts 0 → N over 1.2 s, then the medal appears (soft steady glow), then the breakdown lines slide in one
    /// by one, then the coach advice. "NEW BEST" shows when the score beats the stored best.
    /// Buttons: <b>Next level</b> is primary when passed (<b>Graduate</b> on the last level), <b>Retry</b> is primary when not
    /// passed and names what to fix. Presentation only: scores come from <see cref="ScoreReports"/> (P2 adapter) and
    /// nothing here changes the result or progress.
    /// OWNER: XR & Interaction. Show/ShowCoachText signatures fixed by CONTRACT.md §7.
    /// </summary>
    public class DebriefPanel : MonoBehaviour
    {
        public const float CountUpSeconds = 1.2f;
        public const float MedalSeconds = 0.35f;
        public const float LineSeconds = 0.25f;
        public const int LastLevel = 8;

        [SerializeField] private Text outcomeLabel;
        [SerializeField] private Text detailsLabel;
        [SerializeField] private Text coachHeading;
        [SerializeField] private Text coachLabel;
        [Header("Score reveal (optional)")]
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text medalLabel;
        [SerializeField] private Graphic medalBadge;
        [SerializeField] private Graphic medalGlow;
        [SerializeField] private Text newBestLabel;
        [SerializeField] private Text[] breakdownLines = new Text[0];
        [Header("Buttons (optional)")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button hubButton;
        [SerializeField] private CertificatePanel certificate;
        [SerializeField] private UnityEvent retry = new UnityEvent();
        [SerializeField] private UnityEvent backToHub = new UnityEvent();
        [SerializeField] private UnityEvent nextLevel = new UnityEvent();
        [SerializeField] private UnityEvent graduate = new UnityEvent();

        private Coroutine reveal;
        private string pendingCoach;
        private bool pendingCoachIsAi;
        private bool revealing;
        private bool lastLevel;

        public UnityEvent onRetry => retry;
        public UnityEvent onBackToHub => backToHub;
        /// <summary>Next level (the Integration Lead wires it to load level N+1).</summary>
        public UnityEvent onNextLevel => nextLevel;
        /// <summary>Last level passed: Graduate (opens the certificate when one is assigned).</summary>
        public UnityEvent onGraduate => graduate;
        public ScoreReport LastReport { get; private set; }
        public bool LastWasNewBest { get; private set; }
        public bool IsRevealing => revealing;

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

        public void ConfigureReveal(Text score, Text medal, Graphic badge, Graphic glow, Text newBest, Text[] lines)
        {
            scoreLabel = score;
            medalLabel = medal;
            medalBadge = badge;
            medalGlow = glow;
            newBestLabel = newBest;
            breakdownLines = lines ?? new Text[0];
        }

        public void ConfigureButtons(Button retryControl, Button nextControl, Button hubControl, CertificatePanel certificatePanel = null)
        {
            retryButton = retryControl;
            nextButton = nextControl;
            hubButton = hubControl;
            certificate = certificatePanel;
        }

        public virtual void Show(SessionResult result, MissionDefinition mission)
        {
            if (!ValidateReferences())
                return;
            PrepareText();
            outcomeLabel.enabled = true;
            detailsLabel.enabled = true;
            ClearCoach();
            StopReveal();
            if (result == null || mission == null ||
                !string.Equals(result.missionId, mission.missionId, StringComparison.Ordinal))
            {
                outcomeLabel.text = "Debrief unavailable";
                detailsLabel.text = string.Empty;
                LastReport = null;
                ResetReveal();
                Debug.LogError($"[DebriefPanel] {name}: result and mission are required and must have matching missionId values.", this);
                return;
            }

            outcomeLabel.text = result.passed ? "PASSED" : "NOT PASSED";
            outcomeLabel.color = result.passed ? Theme.Success : Theme.Error;
            detailsLabel.text = DebriefTextFormatter.Format(result, mission);

            var report = ScoreReports.Build(result, mission);
            LastReport = report;
            var best = SafeBest(mission.level);
            LastWasNewBest = result.passed && report.Score > best;
            lastLevel = mission.level >= LastLevel;
            ApplyButtons(result, mission);

            ResetReveal();
            if (Application.isPlaying && isActiveAndEnabled && HasRevealParts())
                reveal = StartCoroutine(Reveal(report));
            else
                ShowFinal(report);
        }

        /// <param name="isAi">True when the text came from the AI coach; the panel must label it "AI coach (advisory)".</param>
        public virtual void ShowCoachText(string text, bool isAi)
        {
            if (!ValidateReferences())
                return;
            PrepareText();
            if (string.IsNullOrWhiteSpace(text))
            {
                pendingCoach = null;
                ClearCoach();
                return;
            }
            if (revealing)
            {
                // The coach advice is the last beat of the reveal.
                pendingCoach = text;
                pendingCoachIsAi = isAi;
                return;
            }
            coachHeading.text = isAi ? "AI coach (advisory)" : "Coach";
            coachLabel.text = text;
            coachHeading.enabled = true;
            coachLabel.enabled = true;
        }

        public void RequestRetry() => retry.Invoke();
        public void RequestBackToHub() => backToHub.Invoke();

        public void RequestNextLevel()
        {
            if (lastLevel)
            {
                if (certificate != null)
                    certificate.Show(AcademyProgress.Current);
                graduate.Invoke();
                return;
            }
            nextLevel.Invoke();
        }

        /// <summary>Skips the animation to its final state (e.g. a tap during the reveal).</summary>
        public void CompleteReveal()
        {
            if (!revealing || LastReport == null) return;
            StopReveal();
            ShowFinal(LastReport);
        }

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
            ResetReveal();
        }

        private void OnDisable()
        {
            if (revealing && LastReport != null)
            {
                StopReveal();
                ShowFinal(LastReport);
            }
        }

        private IEnumerator Reveal(ScoreReport report)
        {
            revealing = true;
            // 1. Score counts up.
            for (var t = 0f; t < CountUpSeconds; t += Time.unscaledDeltaTime)
            {
                var eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / CountUpSeconds), 3f);
                SetScore(Mathf.RoundToInt(report.Score * eased));
                yield return null;
            }
            SetScore(report.Score);

            // 2. Medal: scale/fade in once, then a steady glow (no flashing).
            SetMedal(report.Medal, 0f);
            for (var t = 0f; t < MedalSeconds; t += Time.unscaledDeltaTime)
            {
                SetMedal(report.Medal, Mathf.SmoothStep(0f, 1f, t / MedalSeconds));
                yield return null;
            }
            SetMedal(report.Medal, 1f);
            if (newBestLabel != null) newBestLabel.enabled = LastWasNewBest;

            // 3. Breakdown lines slide in one by one.
            var count = Mathf.Min(breakdownLines.Length, report.Lines.Count);
            for (var i = 0; i < count; i++)
            {
                var line = breakdownLines[i];
                if (line == null) continue;
                line.text = report.Lines[i].ToString();
                line.color = report.Lines[i].Points < 0 ? Theme.WarningInk : Theme.Ink;
                line.enabled = true;
                var rect = line.rectTransform;
                var rest = rect.anchoredPosition;
                for (var t = 0f; t < LineSeconds; t += Time.unscaledDeltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, t / LineSeconds);
                    rect.anchoredPosition = rest + new Vector2(40f * (1f - k), 0f);
                    SetAlpha(line, k);
                    yield return null;
                }
                rect.anchoredPosition = rest;
                SetAlpha(line, 1f);
            }

            // 4. Coach advice.
            revealing = false;
            reveal = null;
            FlushCoach();
        }

        private void ShowFinal(ScoreReport report)
        {
            revealing = false;
            SetScore(report.Score);
            SetMedal(report.Medal, 1f);
            if (newBestLabel != null) newBestLabel.enabled = LastWasNewBest;
            for (var i = 0; i < breakdownLines.Length; i++)
            {
                var line = breakdownLines[i];
                if (line == null) continue;
                var has = i < report.Lines.Count;
                line.enabled = has;
                line.text = has ? report.Lines[i].ToString() : string.Empty;
                if (has) line.color = report.Lines[i].Points < 0 ? Theme.WarningInk : Theme.Ink;
            }
            FlushCoach();
        }

        private void FlushCoach()
        {
            if (pendingCoach == null) return;
            var text = pendingCoach;
            pendingCoach = null;
            ShowCoachText(text, pendingCoachIsAi);
        }

        private void ApplyButtons(SessionResult result, MissionDefinition mission)
        {
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(result.passed);
                SetCaption(nextButton, lastLevel ? "GRADUATE" : "NEXT LEVEL");
                SetPrimary(nextButton, result.passed);
            }
            if (retryButton != null)
            {
                SetCaption(retryButton, result.passed ? "RETRY" : RetryCaption(result, mission));
                SetPrimary(retryButton, !result.passed);
            }
            if (hubButton != null)
                SetPrimary(hubButton, false);
        }

        /// <summary>"RETRY: FOCUS ON STEP 3": the first missed critical step, else the first out-of-order step.</summary>
        public static string RetryCaption(SessionResult result, MissionDefinition mission)
        {
            var focus = FirstStepOrder(result?.omittedCriticalSteps, mission);
            if (focus < 0) focus = FirstStepOrder(result?.sequenceErrors, mission);
            return focus > 0 ? $"RETRY: FOCUS ON STEP {focus}" : "RETRY";
        }

        private static int FirstStepOrder(IList<string> ids, MissionDefinition mission)
        {
            if (ids == null || ids.Count == 0 || mission?.steps == null) return -1;
            var best = int.MaxValue;
            foreach (var id in ids)
                foreach (var step in mission.steps)
                    if (step != null && step.stepId == id && step.order < best)
                        best = step.order;
            return best == int.MaxValue ? -1 : best;
        }

        private static void SetCaption(Button button, string caption)
        {
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.text = caption;
        }

        private static void SetPrimary(Button button, bool primary)
        {
            var colors = button.colors;
            colors.normalColor = primary ? Theme.Accent : Theme.ButtonFill;
            colors.highlightedColor = primary ? Color.Lerp(Theme.Accent, Color.white, 0.25f) : Color.Lerp(Theme.ButtonFill, Theme.Accent, 0.5f);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            button.transform.localScale = Vector3.one * (primary ? 1f : 0.92f);
        }

        private void SetScore(int value)
        {
            if (scoreLabel != null) scoreLabel.text = value.ToString();
        }

        private void SetMedal(Medal medal, float amount)
        {
            var visible = medal != Medal.None && amount > 0f;
            var color = medal == Medal.Gold ? Theme.Gold : medal == Medal.Silver ? Theme.Silver : Theme.Bronze;
            if (medalLabel != null)
            {
                medalLabel.enabled = visible;
                medalLabel.text = medal == Medal.None ? string.Empty : medal.ToString().ToUpperInvariant();
                SetAlpha(medalLabel, amount);
            }
            if (medalLabel != null)
                medalLabel.color = new Color(color.r, color.g, color.b, amount);   // coloured lettering on a tinted chip
            if (medalBadge != null)
            {
                if (medalBadge.gameObject.activeSelf != visible) medalBadge.gameObject.SetActive(visible);   // hides its edge too
                medalBadge.color = new Color(color.r, color.g, color.b, 0.16f * amount);
                medalBadge.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, amount);
            }
            if (medalGlow != null)
            {
                medalGlow.enabled = visible;
                medalGlow.color = new Color(color.r, color.g, color.b, 0.12f * amount);
            }
        }

        private void ResetReveal()
        {
            SetScore(0);
            SetMedal(Medal.None, 0f);
            if (newBestLabel != null) newBestLabel.enabled = false;
            foreach (var line in breakdownLines)
                if (line != null)
                {
                    line.text = string.Empty;
                    line.enabled = false;
                }
        }

        private void StopReveal()
        {
            if (reveal != null) StopCoroutine(reveal);
            reveal = null;
            revealing = false;
        }

        private bool HasRevealParts() => scoreLabel != null || medalLabel != null || breakdownLines.Length > 0;

        private static int SafeBest(int level)
        {
            try { return AcademyProgress.Current.BestScore(level); }
            catch (Exception) { return -1; }
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var c = graphic.color;
            graphic.color = new Color(c.r, c.g, c.b, alpha);
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

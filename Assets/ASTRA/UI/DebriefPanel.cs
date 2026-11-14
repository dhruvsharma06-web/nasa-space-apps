using Astra.Core.Assessment;
using Astra.Core.Mission;
using UnityEngine;

namespace Astra.UI
{
    /// <summary>
    /// Pass/retry outcome + actionable feedback (FR-AS-04).
    /// OWNER: XR & Interaction. Signatures fixed by CONTRACT.md §7.
    /// </summary>
    public class DebriefPanel : MonoBehaviour
    {
        public virtual void Show(SessionResult result, MissionDefinition mission) =>
            Debug.Log($"[Debrief] {mission.missionId}: {(result.passed ? "PASS" : "RETRY")}");

        /// <param name="isAi">True when the text came from the AI coach; the panel must label it "AI coach (advisory)".</param>
        public virtual void ShowCoachText(string text, bool isAi) =>
            Debug.Log($"[Debrief] {(isAi ? "AI coach (advisory)" : "Coach")}: {text}");
    }
}

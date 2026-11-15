using System;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;

namespace Astra.Integrations.Coach
{
    /// <summary>Request body for POST /api/v1/coach/debrief (CONTRACT.md §9).</summary>
    [Serializable]
    public sealed class CoachRequest
    {
        public string sessionId;
        public string missionId;
        public SessionResult result;
        public List<SessionEvent> events = new List<SessionEvent>();
    }

    /// <summary>Response body (CONTRACT.md §9). Treat a null or empty summary as "no advice".</summary>
    [Serializable]
    public sealed class CoachResponse
    {
        public string sessionId;
        public string missionId;
        public bool advisory = true;
        public string summary;
        public List<string> hints = new List<string>();
        public string reason;
        public string modelVersion;
    }

    /// <summary>Request body for POST /api/v1/coach/ask (CONTRACT.md §9.2, Practice only).</summary>
    [Serializable]
    public sealed class CoachAskRequest
    {
        public string sessionId;
        public string missionId;
        /// <summary>Must be "Practice"; any other phase is refused (never available in Qualification).</summary>
        public string phase = "Practice";
        /// <summary>stepId of the current step, or null/empty when every step is done.</summary>
        public string currentStepId;
        /// <summary>Trainee text, 1-300 chars. Untrusted: sent as data only.</summary>
        public string question;
    }

    /// <summary>Response body for the ask endpoint (CONTRACT.md §9.2).</summary>
    [Serializable]
    public sealed class CoachAskResponse
    {
        public string sessionId;
        public string missionId;
        public bool advisory = true;
        public string answer;
        public string stepId;
        public string modelVersion;
    }

    /// <summary>
    /// OWNER: Content & AI Coach. Implementations: OfflineCoach (always works, no network) and
    /// HttpCoachClient (calls the proxy; on any error or timeout falls back to OfflineCoach).
    /// onDone is always called exactly once and never with null. Never throws.
    /// Coach output is advisory: it never changes scoring or simulation state (FR-AI-04, FR-SIM-04).
    /// </summary>
    public interface ICoachClient
    {
        void RequestDebrief(CoachRequest request, MissionDefinition mission, Action<CoachResponse> onDone);
    }

    /// <summary>
    /// Ask-the-coach (Practice only, CONTRACT.md §9.2). Same guarantees as <see cref="ICoachClient"/>:
    /// onDone is called exactly once, never with null, and the call never throws.
    /// </summary>
    public interface ICoachAskClient
    {
        void AskCoach(CoachAskRequest request, MissionDefinition mission, Action<CoachAskResponse> onDone);
    }
}

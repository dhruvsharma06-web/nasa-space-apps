using System;
using System.Collections.Generic;
using Astra.Core.Mission;

namespace Astra.Core.Assessment
{
    /// <summary>
    /// Deterministic scoring (FR-AS-02/03, FR-AI-04, CONTRACT.md §5). Same mission + same trace
    /// always gives the same result (NFR-QUAL-01). Throws ArgumentException on any invalid event.
    /// AI output never feeds into this.
    /// </summary>
    public interface IMissionScorer
    {
        SessionResult Score(MissionDefinition mission, string sessionId, IReadOnlyList<SessionEvent> trace);
    }

    /// <summary>Result of one Qualification attempt. Shape is fixed by CONTRACT.md §5.</summary>
    [Serializable]
    public sealed class SessionResult
    {
        public string sessionId;
        public string missionId;
        public bool passed;
        public List<string> completedSteps = new List<string>();
        public List<string> skippedSteps = new List<string>();
        public List<string> omittedCriticalSteps = new List<string>();
        public List<string> sequenceErrors = new List<string>();
        public List<string> incorrectActions = new List<string>();
        public long completionTimeMs;
    }
}

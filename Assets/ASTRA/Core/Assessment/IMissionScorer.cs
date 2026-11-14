using System.Collections.Generic;
using Astra.Core.Mission;

namespace Astra.Core.Assessment
{
    /// <summary>
    /// Deterministic scoring (FR-AS-02/03, FR-AI-04). Same mission + same event trace
    /// must always give the same result (NFR-QUAL-01). AI never calls this with its own facts.
    /// </summary>
    public interface IMissionScorer
    {
        SessionResult Score(MissionDefinition mission, IReadOnlyList<SessionEvent> trace);
    }

    public sealed class SessionResult
    {
        public bool Passed;
        public List<string> CompletedSteps = new List<string>();
        public List<string> OmittedCriticalSteps = new List<string>();
        public List<string> SequenceErrors = new List<string>();
        public List<string> IncorrectActions = new List<string>();
        public long CompletionTimeMs;
    }
}

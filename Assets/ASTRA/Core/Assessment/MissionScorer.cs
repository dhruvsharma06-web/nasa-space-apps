using System;
using System.Collections.Generic;
using Astra.Core.Mission;

namespace Astra.Core.Assessment
{
    /// <summary>
    /// Deterministic scoring, CONTRACT.md §5. A pure function: same mission + same trace gives an identical
    /// SessionResult every time (NFR-QUAL-01). No clock, randomness, I/O or Unity APIs, and AI output never enters.
    /// Mirrors score() in scripts/validate_content.py, the oracle that CI checks every fixture against.
    /// </summary>
    public sealed class MissionScorer : IMissionScorer
    {
        /// <exception cref="ArgumentException">
        /// The mission is invalid (see MissionRunner), sessionId is empty, the trace is null, or any event
        /// breaks CONTRACT.md §1 (the message names the first bad event and field).
        /// </exception>
        public SessionResult Score(MissionDefinition mission, string sessionId, IReadOnlyList<SessionEvent> trace)
        {
            var steps = StepMatcher.SortedSteps(mission);
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("sessionId is required", nameof(sessionId));
            if (trace == null)
                throw new ArgumentNullException(nameof(trace));

            // Validate everything before scoring anything.
            long previous = 0;
            for (var i = 0; i < trace.Count; i++)
            {
                var error = EventValidator.Check(trace[i], mission.missionId, previous);
                if (error != null)
                    throw new ArgumentException($"trace[{i}]: {error}", nameof(trace));
                previous = trace[i].timestampMs;
            }

            var result = new SessionResult { sessionId = sessionId, missionId = mission.missionId };

            // Recompute every outcome with the shared rule; the stored 'result' field is never trusted.
            var pointer = 0;
            for (var i = 0; i < trace.Count; i++)
            {
                var e = trace[i];
                var match = StepMatcher.Evaluate(steps, pointer, e.@event, e.targetId);
                switch (match.Outcome)
                {
                    case StepOutcome.Correct:
                        for (var j = pointer; j < match.StepIndex; j++)
                            result.skippedSteps.Add(steps[j].stepId);
                        result.completedSteps.Add(steps[match.StepIndex].stepId);
                        pointer = match.StepIndex + 1;
                        break;
                    case StepOutcome.OutOfOrder:
                        result.sequenceErrors.Add(steps[match.StepIndex].stepId);
                        break;
                    case StepOutcome.Incorrect:
                        result.incorrectActions.Add(e.targetId);
                        break;
                }
            }

            // Steps never reached: critical ones are omitted (fail), optional ones are skipped.
            for (var j = pointer; j < steps.Count; j++)
            {
                if (steps[j].critical)
                    result.omittedCriticalSteps.Add(steps[j].stepId);
                else
                    result.skippedSteps.Add(steps[j].stepId);
            }

            result.passed = result.omittedCriticalSteps.Count == 0;
            result.completionTimeMs = trace.Count > 0 ? trace[trace.Count - 1].timestampMs : 0;
            return result;
        }
    }
}

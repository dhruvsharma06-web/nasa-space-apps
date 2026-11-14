using System;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Assessment;
using Astra.Core.Mission;

namespace Astra.App
{
    /// <summary>
    /// Stand-in runner used until Core Logic ships <c>Astra.Core.Mission.MissionRunner</c>.
    /// Simplified rule: only the exact current step is Correct (optional steps are never skipped).
    /// The real rule is CONTRACT.md §4; the LevelController switches to the real class automatically.
    /// OWNER: Integration Lead.
    /// </summary>
    public sealed class MockMissionRunner : IMissionRunner
    {
        private readonly List<MissionStep> _steps;
        private readonly List<SessionEvent> _events = new List<SessionEvent>();
        private int _pointer;

        public MockMissionRunner(MissionDefinition mission, string sessionId)
        {
            if (mission == null || mission.steps == null || mission.steps.Count == 0)
                throw new ArgumentException("mission has no steps");
            Mission = mission;
            SessionId = sessionId;
            _steps = mission.steps.OrderBy(s => s.order).ToList();
        }

        public string SessionId { get; }
        public MissionDefinition Mission { get; }
        public MissionStep CurrentStep => _pointer < _steps.Count ? _steps[_pointer] : null;
        public bool IsComplete => _pointer >= _steps.Count;
        public IReadOnlyList<SessionEvent> Events => _events;

        public StepFeedback Report(string eventName, string targetId, string input, long timestampMs)
        {
            if (Array.IndexOf(EventNames.All, eventName) < 0) throw new ArgumentException($"event '{eventName}' is not a contract event");
            if (Array.IndexOf(InputModality.All, input) < 0) throw new ArgumentException($"input '{input}' is not a contract input");
            if (string.IsNullOrEmpty(targetId)) throw new ArgumentException("targetId is required");
            long last = _events.Count > 0 ? _events[_events.Count - 1].timestampMs : 0;
            if (timestampMs < 0 || timestampMs < last) throw new ArgumentException("timestampMs must be >= 0 and not decrease");

            StepOutcome outcome;
            string stepId = null;
            var current = CurrentStep;
            if (current != null && current.expectedEvent == eventName && current.targetId == targetId)
            {
                outcome = StepOutcome.Correct; stepId = current.stepId; _pointer++;
            }
            else if (_steps.Skip(_pointer + 1).FirstOrDefault(s => s.expectedEvent == eventName && s.targetId == targetId) is MissionStep later)
            {
                outcome = StepOutcome.OutOfOrder; stepId = later.stepId;
            }
            else if (Array.IndexOf(EventNames.NeutralWhenUnmatched, eventName) >= 0 ||
                     _steps.Take(_pointer).Any(s => s.expectedEvent == eventName && s.targetId == targetId))
            {
                outcome = StepOutcome.Ignored;
            }
            else
            {
                outcome = StepOutcome.Incorrect;
            }

            _events.Add(new SessionEvent
            {
                sessionId = SessionId, missionId = Mission.missionId, timestampMs = timestampMs,
                @event = eventName, targetId = targetId, input = input,
                result = outcome == StepOutcome.Correct ? EventResults.Correct
                       : outcome == StepOutcome.Ignored ? EventResults.Neutral : EventResults.Incorrect,
            });
            return new StepFeedback { Outcome = outcome, StepId = stepId, Hint = CurrentStep?.hint };
        }
    }

    /// <summary>Stand-in scorer (replays the trace through <see cref="MockMissionRunner"/>). Replaced by Core's MissionScorer.</summary>
    public sealed class MockMissionScorer : IMissionScorer
    {
        public SessionResult Score(MissionDefinition mission, string sessionId, IReadOnlyList<SessionEvent> trace)
        {
            var runner = new MockMissionRunner(mission, sessionId);
            var result = new SessionResult { sessionId = sessionId, missionId = mission.missionId };
            foreach (var e in trace)
            {
                var fb = runner.Report(e.@event, e.targetId, e.input, e.timestampMs);
                if (fb.Outcome == StepOutcome.Correct) result.completedSteps.Add(fb.StepId);
                else if (fb.Outcome == StepOutcome.OutOfOrder) result.sequenceErrors.Add(fb.StepId);
                else if (fb.Outcome == StepOutcome.Incorrect) result.incorrectActions.Add(e.targetId);
            }
            foreach (var s in mission.steps.OrderBy(s => s.order).Where(s => !result.completedSteps.Contains(s.stepId)))
                (s.critical ? result.omittedCriticalSteps : result.skippedSteps).Add(s.stepId);
            result.passed = result.omittedCriticalSteps.Count == 0;
            result.completionTimeMs = trace.Count > 0 ? trace[trace.Count - 1].timestampMs : 0;
            return result;
        }
    }
}

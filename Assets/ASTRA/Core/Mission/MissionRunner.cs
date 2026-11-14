using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Astra.Core.Assessment;

namespace Astra.Core.Mission
{
    /// <summary>
    /// Live step tracking for one Practice or Qualification attempt (CONTRACT.md §4).
    /// Create a fresh runner per attempt. Uses StepMatcher, the same rule as MissionScorer,
    /// so the live feedback and the final score always agree.
    /// Pure C#: no clock, randomness, I/O or Unity APIs; the caller supplies every timestamp.
    /// </summary>
    public sealed class MissionRunner : IMissionRunner
    {
        private readonly List<MissionStep> _steps;
        private readonly List<SessionEvent> _events = new List<SessionEvent>();
        private readonly ReadOnlyCollection<SessionEvent> _eventsView;
        private int _pointer;
        private long _lastTimestampMs;

        /// <exception cref="ArgumentException">
        /// The mission is null, has no steps, a null step or duplicate order values, or sessionId is empty.
        /// </exception>
        public MissionRunner(MissionDefinition mission, string sessionId)
        {
            _steps = StepMatcher.SortedSteps(mission);
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("sessionId is required", nameof(sessionId));

            Mission = mission;
            SessionId = sessionId;
            _eventsView = _events.AsReadOnly();
        }

        public string SessionId { get; }

        public MissionDefinition Mission { get; }

        public MissionStep CurrentStep => _pointer < _steps.Count ? _steps[_pointer] : null;

        public bool IsComplete => _pointer >= _steps.Count;

        public IReadOnlyList<SessionEvent> Events => _eventsView;

        /// <summary>
        /// Records one interaction and returns what it meant. A rejected report (ArgumentException)
        /// changes nothing: no event is logged and the current step stays the same.
        /// </summary>
        public StepFeedback Report(string eventName, string targetId, string input, long timestampMs)
        {
            var error = EventValidator.CheckAction(eventName, targetId, input, timestampMs, _lastTimestampMs);
            if (error != null)
                throw new ArgumentException(error);

            var match = StepMatcher.Evaluate(_steps, _pointer, eventName, targetId);
            if (match.Outcome == StepOutcome.Correct)
                _pointer = match.StepIndex + 1;

            _events.Add(new SessionEvent
            {
                sessionId = SessionId,
                missionId = Mission.missionId,
                timestampMs = timestampMs,
                @event = eventName,
                targetId = targetId,
                input = input,
                result = StepMatcher.ResultFor(match.Outcome),
            });
            _lastTimestampMs = timestampMs;

            return new StepFeedback
            {
                Outcome = match.Outcome,
                StepId = match.StepIndex >= 0 ? _steps[match.StepIndex].stepId : null,
                Hint = CurrentStep?.hint,
            };
        }
    }
}

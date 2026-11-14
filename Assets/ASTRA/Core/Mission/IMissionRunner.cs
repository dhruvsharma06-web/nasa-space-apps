using System.Collections.Generic;
using Astra.Core.Assessment;

namespace Astra.Core.Mission
{
    /// <summary>
    /// Live step tracking for one attempt (CONTRACT.md §4). One runner per Practice or Qualification attempt.
    /// Uses the same step-matching rule as the scorer, so live feedback and the final score always agree.
    /// </summary>
    public interface IMissionRunner
    {
        string SessionId { get; }
        MissionDefinition Mission { get; }

        /// <summary>The next step to do, or null when every step is done.</summary>
        MissionStep CurrentStep { get; }

        bool IsComplete { get; }

        /// <summary>Every event recorded so far, with result stamped (correct / incorrect / neutral).</summary>
        IReadOnlyList<SessionEvent> Events { get; }

        /// <summary>
        /// Records one interaction. Throws ArgumentException if eventName/input are not contract values,
        /// targetId is empty, or timestampMs is negative or earlier than the previous event.
        /// </summary>
        StepFeedback Report(string eventName, string targetId, string input, long timestampMs);
    }

    public enum StepOutcome
    {
        /// <summary>Matched the current step (or a later step with only non-critical steps skipped).</summary>
        Correct,
        /// <summary>Wrong target/action. Logged with result "incorrect".</summary>
        Incorrect,
        /// <summary>Matches a later step but a critical step is still pending. Logged "incorrect"; no advance.</summary>
        OutOfOrder,
        /// <summary>Neutral or repeated action. Logged "neutral".</summary>
        Ignored
    }

    public sealed class StepFeedback
    {
        public StepOutcome Outcome;

        /// <summary>The step this event matched (Correct / OutOfOrder), otherwise null.</summary>
        public string StepId;

        /// <summary>Hint of the current pending step, for Practice feedback. Null when complete.</summary>
        public string Hint;
    }
}

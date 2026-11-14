using System;
using System.Collections.Generic;
using Astra.Core.Assessment;

namespace Astra.Core.Mission
{
    /// <summary>
    /// The step-matching rule, CONTRACT.md §4. This is the only implementation: the mission runner (live feedback)
    /// and the scorer (final result) both call it, so they can never disagree.
    /// Mirrors match() in scripts/validate_content.py, which CI uses to verify every fixture.
    /// </summary>
    internal static class StepMatcher
    {
        internal readonly struct Match
        {
            public readonly StepOutcome Outcome;

            /// <summary>Index of the matched step in the sorted list (Correct / OutOfOrder), otherwise -1.</summary>
            public readonly int StepIndex;

            public Match(StepOutcome outcome, int stepIndex)
            {
                Outcome = outcome;
                StepIndex = stepIndex;
            }
        }

        /// <summary>
        /// Classifies one event. <paramref name="pointer"/> is the index of the first pending step.
        /// Does not change any state; the caller advances the pointer on Correct (to StepIndex + 1).
        /// </summary>
        public static Match Evaluate(List<MissionStep> steps, int pointer, string eventName, string targetId)
        {
            // 1. First pending-or-later step with the same expectedEvent and targetId.
            for (var k = pointer; k < steps.Count; k++)
            {
                if (!Matches(steps[k], eventName, targetId))
                    continue;

                for (var j = pointer; j < k; j++)
                {
                    if (steps[j].critical)
                        return new Match(StepOutcome.OutOfOrder, k);
                }
                return new Match(StepOutcome.Correct, k);
            }

            // 2. Repeat of a step that is already behind the pointer (done or skipped).
            for (var i = 0; i < pointer && i < steps.Count; i++)
            {
                if (Matches(steps[i], eventName, targetId))
                    return new Match(StepOutcome.Ignored, -1);
            }

            // 3. Neutral actions (look, grab, release, pause, resume, recenter) are never wrong on their own.
            if (EventValidator.IsOneOf(eventName, EventNames.NeutralWhenUnmatched))
                return new Match(StepOutcome.Ignored, -1);

            // 4. Anything else is a wrong action.
            return new Match(StepOutcome.Incorrect, -1);
        }

        /// <summary>The result string the runner stamps on the logged event.</summary>
        public static string ResultFor(StepOutcome outcome)
        {
            switch (outcome)
            {
                case StepOutcome.Correct: return EventResults.Correct;
                case StepOutcome.Ignored: return EventResults.Neutral;
                default: return EventResults.Incorrect; // Incorrect and OutOfOrder
            }
        }

        /// <summary>
        /// The mission's steps sorted by order (stable). Throws ArgumentException if the mission is null,
        /// has no steps, has a null step, or has duplicate order values.
        /// </summary>
        public static List<MissionStep> SortedSteps(MissionDefinition mission)
        {
            if (mission == null)
                throw new ArgumentNullException(nameof(mission));
            if (mission.steps == null || mission.steps.Count == 0)
                throw new ArgumentException($"mission '{mission.missionId}' has no steps", nameof(mission));

            var sorted = new List<MissionStep>(mission.steps.Count);
            foreach (var step in mission.steps)
            {
                if (step == null)
                    throw new ArgumentException($"mission '{mission.missionId}' has a null step", nameof(mission));

                // Insertion sort: stable (equal keys keep file order) and plenty fast for a handful of steps.
                var at = sorted.Count;
                while (at > 0 && sorted[at - 1].order > step.order)
                    at--;
                if (at > 0 && sorted[at - 1].order == step.order)
                    throw new ArgumentException(
                        $"mission '{mission.missionId}' has two steps with order {step.order} ('{sorted[at - 1].stepId}', '{step.stepId}')",
                        nameof(mission));
                sorted.Insert(at, step);
            }
            return sorted;
        }

        private static bool Matches(MissionStep step, string eventName, string targetId) =>
            step.expectedEvent == eventName && step.targetId == targetId;
    }
}

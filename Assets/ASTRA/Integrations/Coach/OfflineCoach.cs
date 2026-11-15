using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Astra.Core.Assessment;
using Astra.Core.Mission;

namespace Astra.Integrations.Coach
{
    /// <summary>
    /// Deterministic offline coach (CONTRACT.md §9). A port of tools/level-sim/coach.js:
    /// same mission + result + events always gives the same text. No network, clock or randomness.
    /// All advice is built only from the mission file; it never invents procedures.
    /// Calm flight-instructor tone, no emojis.
    /// </summary>
    public sealed class OfflineCoach : ICoachClient, ICoachAskClient
    {
        public const string ModelVersion = "offline-1";
        public const string NoEventsReason = "NO_EVENTS";
        public const string RefusalAnswer = "I can only help with the current procedure. Check the checklist for the current step.";
        public const string AllDoneAnswer = "All steps are complete.";

        private const int MaxHints = 3;
        private const int MaxHintChars = 200;
        private const int MaxSummaryChars = 600;
        private const int MaxAnswerChars = 400;
        private const int MaxAnswerWords = 60;
        private const string Ellipsis = "…";

        // ---------------- ICoachClient ----------------

        public void RequestDebrief(CoachRequest request, MissionDefinition mission, Action<CoachResponse> onDone)
        {
            if (onDone == null) return;
            onDone(Debrief(request, mission));
        }

        /// <summary>Synchronous offline debrief. Never returns null, never throws.</summary>
        public CoachResponse Debrief(CoachRequest request, MissionDefinition mission)
        {
            var response = new CoachResponse
            {
                sessionId = request != null ? request.sessionId : null,
                missionId = request != null ? request.missionId : null,
                advisory = true,
                modelVersion = ModelVersion,
            };
            try
            {
                if (request == null || request.events == null || request.events.Count == 0 || request.result == null)
                {
                    response.summary = null;
                    response.reason = NoEventsReason;
                    return response;
                }

                var result = request.result;
                var byId = new Dictionary<string, MissionStep>();
                var steps = SortedSteps(mission);
                foreach (var s in steps)
                    if (s.stepId != null && !byId.ContainsKey(s.stepId)) byId[s.stepId] = s;

                var hints = response.hints;
                void Push(string h)
                {
                    if (string.IsNullOrEmpty(h) || hints.Contains(h) || hints.Count >= MaxHints) return;
                    hints.Add(Clip(h, MaxHintChars));
                }

                // 1. Every omitted critical step: its own hint from the mission file.
                foreach (var id in result.omittedCriticalSteps ?? new List<string>())
                {
                    MissionStep step;
                    if (id != null && byId.TryGetValue(id, out step)) Push(step.hint);
                }

                // 2. Every sequence error: which step had to come first.
                foreach (var pair in PendingBeforeEachSequenceError(steps, request.events))
                {
                    Push("Order matters: \"" + Unquote(pair.Pending.instruction) + "\" comes before \""
                         + Unquote(pair.Early.instruction) + "\".");
                }

                // 3. Wrong controls: one general tip.
                int wrong = result.incorrectActions != null ? result.incorrectActions.Count : 0;
                if (wrong > 0)
                {
                    Push("Check the panel label before you act: " + Plural(wrong, "action")
                         + " used a control that was not part of the current step.");
                }

                response.summary = Clip(Summary(result), MaxSummaryChars);
                return response;
            }
            catch (Exception)
            {
                // Never throw: degrade to "no advice".
                response.summary = null;
                response.hints = new List<string>();
                response.reason = NoEventsReason;
                return response;
            }
        }

        /// <summary>Summary sentence(s), CONTRACT.md §9 limits apply.</summary>
        public static string Summary(SessionResult result)
        {
            int wrong = result.incorrectActions != null ? result.incorrectActions.Count : 0;
            int order = result.sequenceErrors != null ? result.sequenceErrors.Count : 0;
            int omitted = result.omittedCriticalSteps != null ? result.omittedCriticalSteps.Count : 0;
            string mistakes = Plural(wrong, "wrong action") + " and " + Plural(order, "out-of-order action") + " recorded.";
            if (result.passed && wrong == 0 && order == 0) return "Passed. All critical steps completed with no errors.";
            if (result.passed) return "Passed. All critical steps completed; " + mistakes;
            return "Not passed. " + Plural(omitted, "critical step") + " not completed; " + mistakes;
        }

        // ---------------- ICoachAskClient ----------------

        public void AskCoach(CoachAskRequest request, MissionDefinition mission, Action<CoachAskResponse> onDone)
        {
            if (onDone == null) return;
            onDone(Ask(request, mission));
        }

        /// <summary>
        /// Synchronous offline answer: the current step's hint plus where to look.
        /// Never returns null, never throws. The question text is never used.
        /// </summary>
        public CoachAskResponse Ask(CoachAskRequest request, MissionDefinition mission)
        {
            var response = new CoachAskResponse
            {
                sessionId = request != null ? request.sessionId : null,
                missionId = request != null ? request.missionId : null,
                advisory = true,
                stepId = request != null && !string.IsNullOrEmpty(request.currentStepId) ? request.currentStepId : null,
                modelVersion = ModelVersion,
            };
            try
            {
                if (request == null || request.phase != "Practice")
                {
                    response.answer = RefusalAnswer;
                    return response;
                }
                if (string.IsNullOrEmpty(request.currentStepId))
                {
                    response.answer = AllDoneAnswer;
                    return response;
                }

                MissionStep current = null;
                foreach (var s in SortedSteps(mission))
                {
                    if (s.stepId == request.currentStepId) { current = s; break; }
                }
                if (current == null)
                {
                    response.answer = RefusalAnswer;
                    return response;
                }

                string tip = !string.IsNullOrEmpty(current.hint) ? current.hint.Trim() : (current.instruction ?? string.Empty).Trim();
                if (tip.Length > 0 && !tip.EndsWith(".", StringComparison.Ordinal) && !tip.EndsWith("!", StringComparison.Ordinal)
                    && !tip.EndsWith("?", StringComparison.Ordinal))
                    tip += ".";
                string lookFor = string.IsNullOrEmpty(current.targetId) ? string.Empty : " Look for the " + Label(current.targetId) + " control.";
                response.answer = Clip(LimitWords(tip + lookFor, MaxAnswerWords), MaxAnswerChars);
                return response;
            }
            catch (Exception)
            {
                response.answer = RefusalAnswer;
                return response;
            }
        }

        // ---------------- internals ----------------

        private enum Outcome { Correct, OutOfOrder, Ignored, Incorrect }

        private static readonly string[] NeutralWhenUnmatched =
            { "target_located", "object_grabbed", "object_released", "paused", "resumed", "recentered" };

        private struct OrderPair
        {
            public MissionStep Pending;
            public MissionStep Early;
        }

        private static List<MissionStep> SortedSteps(MissionDefinition mission)
        {
            if (mission == null || mission.steps == null) return new List<MissionStep>();
            // OrderBy is a stable sort.
            return mission.steps.Where(s => s != null).OrderBy(s => s.order).ToList();
        }

        private static bool Same(MissionStep s, string ev, string target) => s.expectedEvent == ev && s.targetId == target;

        /// <summary>Step-matching rule, CONTRACT.md §4 (same as StepMatcher in ASTRA.Core).</summary>
        private static Outcome Match(List<MissionStep> steps, int pointer, string ev, string target, out int index)
        {
            index = -1;
            for (int k = pointer; k < steps.Count; k++)
            {
                if (!Same(steps[k], ev, target)) continue;
                index = k;
                for (int j = pointer; j < k; j++)
                    if (steps[j].critical) return Outcome.OutOfOrder;
                return Outcome.Correct;
            }
            index = -1;
            for (int i = 0; i < pointer && i < steps.Count; i++)
                if (Same(steps[i], ev, target)) return Outcome.Ignored;
            if (Array.IndexOf(NeutralWhenUnmatched, ev) >= 0) return Outcome.Ignored;
            return Outcome.Incorrect;
        }

        /// <summary>
        /// For each out-of-order event: (blocking step, step attempted too early). The blocking step is the first
        /// CRITICAL step still pending before the early one; optional steps never block (CONTRACT.md §4).
        /// </summary>
        private static List<OrderPair> PendingBeforeEachSequenceError(List<MissionStep> steps, List<SessionEvent> events)
        {
            var pairs = new List<OrderPair>();
            int p = 0;
            foreach (var e in events)
            {
                if (e == null) continue;
                int k;
                var outcome = Match(steps, p, e.@event, e.targetId, out k);
                if (outcome == Outcome.Correct) p = k + 1;
                else if (outcome == Outcome.OutOfOrder)
                {
                    MissionStep pending = null;
                    for (int j = p; j < k; j++)
                        if (steps[j].critical) { pending = steps[j]; break; }
                    if (pending != null) pairs.Add(new OrderPair { Pending = pending, Early = steps[k] });
                }
            }
            return pairs;
        }

        private static string Plural(int n, string word) => n + " " + word + (n == 1 ? string.Empty : "s");

        private static string Unquote(string s) => Regex.Replace(s ?? string.Empty, @"\.\s*$", string.Empty);

        private static string Clip(string s, int n)
        {
            if (s == null) return null;
            return s.Length <= n ? s : s.Substring(0, n - 1).TrimEnd() + Ellipsis;
        }

        /// <summary>"main_breaker" becomes "MAIN BREAKER".</summary>
        private static string Label(string targetId) => targetId.Replace('_', ' ').ToUpperInvariant();

        private static string LimitWords(string s, int maxWords)
        {
            var words = s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= maxWords) return string.Join(" ", words);
            var sb = new StringBuilder(string.Join(" ", words, 0, maxWords).TrimEnd('.', ',', ';', ':'));
            sb.Append('.');
            return sb.ToString();
        }
    }
}

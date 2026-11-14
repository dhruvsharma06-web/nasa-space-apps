using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Astra.Core.Mission;

namespace Astra.Core.Assessment
{
    /// <summary>Medal for one attempt. <see cref="None"/> = not passed.</summary>
    public enum Rating { None, Bronze, Silver, Gold }

    /// <summary>
    /// A 0–100 player score, its medal and the lines that explain it.
    /// The score always equals 100 plus the sum of the line points.
    /// </summary>
    public readonly struct ScoreBreakdown
    {
        private static readonly IReadOnlyList<(string label, int points)> NoLines =
            new ReadOnlyCollection<(string label, int points)>(new (string, int)[0]);

        private readonly IReadOnlyList<(string label, int points)> _lines;

        public ScoreBreakdown(int score, Rating rating, IReadOnlyList<(string label, int points)> lines)
        {
            Score = score;
            Rating = rating;
            _lines = lines;
        }

        /// <summary>0..100.</summary>
        public int Score { get; }

        public Rating Rating { get; }

        /// <summary>Shown in the debrief, e.g. ("Wrong control: AUX HEATER", -8). Never null.</summary>
        public IReadOnlyList<(string label, int points)> Lines => _lines ?? NoLines;
    }

    /// <summary>
    /// The player-facing score (DECISIONS D9, docs/SCORING.md). Pure and deterministic: it reads a
    /// <see cref="SessionResult"/> and never changes it, so <c>passed</c> stays the scorer's (CONTRACT §5).
    /// <para>Rules:</para>
    /// <list type="bullet">
    /// <item>Start at 100.</item>
    /// <item>−25 per missed critical step; −10 per out-of-order step; −8 per wrong control.</item>
    /// <item>Time: par = 8 s × number of steps. −1 per full 5 s over par, at most −15.</item>
    /// <item>+5 "Swift" when under 70 % of par with no errors (missed critical, out-of-order or wrong control).</item>
    /// <item>The score is capped at 100. If not passed, it is capped at 59. It never goes below 0.</item>
    /// <item>Gold ≥ 90, Silver ≥ 75, Bronze = passed below 75, None = not passed.</item>
    /// </list>
    /// </summary>
    public static class ResultRating
    {
        public const int StartScore = 100;
        public const int MissedCriticalPoints = -25;
        public const int OutOfOrderPoints = -10;
        public const int WrongControlPoints = -8;
        public const long ParMsPerStep = 8000;
        public const long TimeBlockMs = 5000;
        public const int MaxTimePenalty = 15;
        public const int SwiftBonus = 5;
        public const int SwiftParPercent = 70;
        public const int NotPassedCap = 59;
        public const int GoldMin = 90;
        public const int SilverMin = 75;

        private static readonly string[] IdSuffixes =
            { "_button", "_toggle", "_dial", "_lever", "_selector", "_breaker", "_switch", "_display", "_panel" };

        /// <summary>Scores a result. Wrong controls are named from their ids (aux_heater_toggle → AUX HEATER).</summary>
        public static ScoreBreakdown From(SessionResult r, MissionDefinition m) => From(r, m, null);

        /// <summary>
        /// Scores a result. <paramref name="controlLabel"/> (optional) names a wrong control from its
        /// targetId, e.g. with the layout's label; a null or empty answer falls back to the id.
        /// </summary>
        public static ScoreBreakdown From(SessionResult r, MissionDefinition m, Func<string, string> controlLabel)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (m == null) throw new ArgumentNullException(nameof(m));

            var lines = new List<(string label, int points)>();
            int score = StartScore;

            foreach (var stepId in r.omittedCriticalSteps ?? new List<string>())
                Add(lines, ref score, "Missed critical step " + StepNumber(m, stepId), MissedCriticalPoints);
            foreach (var stepId in r.sequenceErrors ?? new List<string>())
                Add(lines, ref score, "Out of order: step " + StepNumber(m, stepId), OutOfOrderPoints);
            foreach (var targetId in r.incorrectActions ?? new List<string>())
                Add(lines, ref score, "Wrong control: " + ControlName(targetId, controlLabel), WrongControlPoints);

            long par = ParMs(m);
            long time = Math.Max(0L, r.completionTimeMs);
            int timePenalty = TimePenalty(time, par);
            if (timePenalty > 0)
                Add(lines, ref score, $"Time: {Seconds(time - par)} s over par of {Seconds(par)} s", -timePenalty);

            int errors = Count(r.omittedCriticalSteps) + Count(r.sequenceErrors) + Count(r.incorrectActions);
            if (errors == 0 && IsSwift(time, par))
                Add(lines, ref score, $"Swift: under {SwiftParPercent}% of par", SwiftBonus);

            if (score > StartScore)
                Add(lines, ref score, "Maximum score is 100", StartScore - score);
            if (!r.passed && score > NotPassedCap)
                Add(lines, ref score, "Not passed: score capped at " + NotPassedCap, NotPassedCap - score);
            if (score < 0)
                Add(lines, ref score, "Minimum score is 0", -score);

            if (lines.Count == 0)
                lines.Add(("Clean run: every step in order", 0));

            return new ScoreBreakdown(score, RatingFor(score, r.passed),
                new ReadOnlyCollection<(string label, int points)>(lines));
        }

        /// <summary>The medal for a final score.</summary>
        public static Rating RatingFor(int score, bool passed)
        {
            if (!passed) return Rating.None;
            if (score >= GoldMin) return Rating.Gold;
            if (score >= SilverMin) return Rating.Silver;
            return Rating.Bronze;
        }

        /// <summary>Par time: 8 s per mission step.</summary>
        public static long ParMs(MissionDefinition m) => ParMsPerStep * Count(m?.steps);

        /// <summary>−1 point per full 5 s over par, at most 15.</summary>
        public static int TimePenalty(long timeMs, long parMs)
        {
            long over = timeMs - parMs;
            if (over < TimeBlockMs) return 0;
            return (int)Math.Min(MaxTimePenalty, over / TimeBlockMs);
        }

        /// <summary>True when strictly under 70 % of par.</summary>
        public static bool IsSwift(long timeMs, long parMs) => parMs > 0 && timeMs * 100 < parMs * SwiftParPercent;

        /// <summary>A readable control name from a targetId: aux_heater_toggle → AUX HEATER.</summary>
        public static string HumanizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            foreach (var suffix in IdSuffixes)
            {
                if (id.Length > suffix.Length && id.EndsWith(suffix, StringComparison.Ordinal))
                {
                    id = id.Substring(0, id.Length - suffix.Length);
                    break;
                }
            }
            return id.Replace('_', ' ').ToUpperInvariant();
        }

        private static void Add(List<(string label, int points)> lines, ref int score, string label, int points)
        {
            score += points;
            lines.Add((label, points));
        }

        private static string StepNumber(MissionDefinition m, string stepId)
        {
            if (m.steps != null)
                foreach (var step in m.steps)
                    if (step != null && step.stepId == stepId)
                        return step.order.ToString(CultureInfo.InvariantCulture);
            return stepId ?? "?";
        }

        private static string ControlName(string targetId, Func<string, string> controlLabel)
        {
            var label = controlLabel?.Invoke(targetId);
            return string.IsNullOrWhiteSpace(label) ? HumanizeId(targetId) : label.Trim();
        }

        private static string Seconds(long ms) => (ms / 1000).ToString(CultureInfo.InvariantCulture);

        private static int Count<T>(ICollection<T> list) => list?.Count ?? 0;
    }
}

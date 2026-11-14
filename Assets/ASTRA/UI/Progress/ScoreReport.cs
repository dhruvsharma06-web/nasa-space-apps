using System;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;

namespace Astra.UI.Progress
{
    public enum Medal { None, Bronze, Silver, Gold }

    /// <summary>One debrief breakdown line, e.g. "Wrong control: AUX HEATER" with -8 points.</summary>
    [Serializable]
    public struct BreakdownLine
    {
        public string Text;
        public int Points;

        public BreakdownLine(string text, int points)
        {
            Text = text;
            Points = points;
        }

        public override string ToString() => Points == 0 ? Text : $"{Text}  {(Points > 0 ? "+" : "")}{Points}";
    }

    /// <summary>What the debrief reveals: a 0–100 score, a medal and its breakdown. Presentation only.</summary>
    public sealed class ScoreReport
    {
        public int Score;
        public Medal Medal;
        public bool Passed;
        public List<BreakdownLine> Lines = new List<BreakdownLine>();
    }

    /// <summary>Adapter for P2's <c>ResultRating</c>/<c>ScoreBreakdown</c> (TASKS S7). P2 replaces <see cref="ScoreReports.Source"/>.</summary>
    public interface IScoreReportSource
    {
        ScoreReport Build(SessionResult result, MissionDefinition mission);
    }

    public static class ScoreReports
    {
        /// <summary>Set by P2 at startup. Until then, <see cref="PlaceholderScoreSource"/> (clearly marked) is used.</summary>
        public static IScoreReportSource Source { get; set; } = new PlaceholderScoreSource();

        public static ScoreReport Build(SessionResult result, MissionDefinition mission) =>
            (Source ?? new PlaceholderScoreSource()).Build(result, mission);
    }

    /// <summary>
    /// PLACEHOLDER until P2's ResultRating ships (docs/SCORING.md): 100 minus fixed penalties, medal by threshold.
    /// It exists only so the debrief and Hub can be built and demoed; it is not the academy's scoring rule.
    /// </summary>
    public sealed class PlaceholderScoreSource : IScoreReportSource
    {
        public ScoreReport Build(SessionResult result, MissionDefinition mission)
        {
            var report = new ScoreReport();
            if (result == null)
                return report;
            report.Passed = result.passed;
            var score = 100;
            foreach (var id in result.omittedCriticalSteps)
                Add(report, ref score, "Missed step: " + StepText(id, mission), -20);
            foreach (var id in result.sequenceErrors)
                Add(report, ref score, "Out of order: " + StepText(id, mission), -5);
            foreach (var id in result.incorrectActions)
                Add(report, ref score, "Wrong control: " + Humanize(id), -8);
            if (report.Lines.Count == 0)
                report.Lines.Add(new BreakdownLine("Clean run: every step in order", 0));
            report.Score = Math.Max(0, Math.Min(100, score));
            report.Medal = !result.passed ? Medal.None : report.Score >= 90 ? Medal.Gold : report.Score >= 75 ? Medal.Silver : Medal.Bronze;
            return report;
        }

        private static void Add(ScoreReport report, ref int score, string text, int points)
        {
            score += points;
            report.Lines.Add(new BreakdownLine(text, points));
        }

        private static string StepText(string stepId, MissionDefinition mission)
        {
            if (mission?.steps != null)
                foreach (var step in mission.steps)
                    if (step != null && step.stepId == stepId)
                        return "step " + step.order;
            return Humanize(stepId);
        }

        internal static string Humanize(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            foreach (var suffix in new[] { "_button", "_toggle", "_dial", "_lever", "_panel" })
                if (id.EndsWith(suffix, StringComparison.Ordinal) && id.Length > suffix.Length)
                {
                    id = id.Substring(0, id.Length - suffix.Length);
                    break;
                }
            return id.Replace('_', ' ').ToUpperInvariant();
        }
    }
}

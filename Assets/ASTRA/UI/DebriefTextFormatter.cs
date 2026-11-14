using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Astra.Core.Assessment;
using Astra.Core.Mission;

namespace Astra.UI
{
    /// <summary>Presentation of an existing result. Never recomputes or mutates assessment data.</summary>
    public static class DebriefTextFormatter
    {
        public static string Format(SessionResult result, MissionDefinition mission)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (mission == null)
                throw new ArgumentNullException(nameof(mission));
            if (!string.Equals(result.missionId, mission.missionId, StringComparison.Ordinal))
                throw new ArgumentException("The result and mission must refer to the same mission.");

            var text = new StringBuilder();
            text.AppendLine(mission.title ?? string.Empty);
            var seconds = Math.Max(0L, result.completionTimeMs) / 1000L;
            text.Append("Time: ").Append((seconds / 60L).ToString("00", CultureInfo.InvariantCulture))
                .Append(':').Append((seconds % 60L).ToString("00", CultureInfo.InvariantCulture));

            AppendSteps(text, "Completed steps", result.completedSteps, mission);
            AppendSteps(text, "Skipped optional steps", result.skippedSteps, mission);
            AppendSteps(text, "Required steps missed", result.omittedCriticalSteps, mission);
            AppendSteps(text, "Sequence errors", result.sequenceErrors, mission);
            AppendTargets(text, result.incorrectActions, mission);
            return text.ToString();
        }

        private static void AppendSteps(StringBuilder text, string heading, IList<string> ids, MissionDefinition mission)
        {
            text.AppendLine().AppendLine().Append(heading).Append(':');
            if (ids == null || ids.Count == 0)
            {
                text.Append(" None");
                return;
            }

            for (var i = 0; i < ids.Count; i++)
                text.AppendLine().Append("- ").Append(FindInstruction(ids[i], mission));
        }

        private static void AppendTargets(StringBuilder text, IList<string> ids, MissionDefinition mission)
        {
            text.AppendLine().AppendLine().Append("Incorrect actions:");
            if (ids == null || ids.Count == 0)
            {
                text.Append(" None");
                return;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                var instruction = FindTargetInstruction(ids[i], mission);
                text.AppendLine().Append("- ").Append(instruction ?? Humanize(ids[i]));
            }
        }

        private static string FindInstruction(string stepId, MissionDefinition mission)
        {
            if (mission.steps != null)
                for (var i = 0; i < mission.steps.Count; i++)
                {
                    var step = mission.steps[i];
                    if (step != null && step.stepId == stepId && !string.IsNullOrWhiteSpace(step.instruction))
                        return step.instruction;
                }
            return Humanize(stepId);
        }

        private static string FindTargetInstruction(string targetId, MissionDefinition mission)
        {
            if (mission.steps != null)
                for (var i = 0; i < mission.steps.Count; i++)
                {
                    var step = mission.steps[i];
                    if (step != null && step.targetId == targetId && !string.IsNullOrWhiteSpace(step.instruction))
                        return step.instruction;
                }
            return null;
        }

        private static string Humanize(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "Unknown action";

            var words = id.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < words.Length; i++)
                if (words[i].Length > 0)
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return words.Length == 0 ? "Unknown action" : string.Join(" ", words);
        }
    }
}

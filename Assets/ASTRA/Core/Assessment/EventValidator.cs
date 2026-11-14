using System;

namespace Astra.Core.Assessment
{
    /// <summary>
    /// Checks events against CONTRACT.md §1. Returns the first problem as a readable message, or null when valid.
    /// Shared by the mission runner (one live action at a time) and the scorer (a whole stored trace),
    /// so both reject exactly the same input. Check order mirrors scripts/validate_content.py.
    /// </summary>
    internal static class EventValidator
    {
        /// <summary>Validates one stored SessionEvent (scorer input).</summary>
        /// <param name="missionId">The mission being scored; the event must belong to it.</param>
        /// <param name="previousTimestampMs">Timestamp of the event before this one (0 for the first).</param>
        public static string Check(SessionEvent e, string missionId, long previousTimestampMs)
        {
            if (e == null)
                return "event is null";
            if (string.IsNullOrEmpty(e.sessionId))
                return "sessionId is required";
            if (e.missionId != missionId)
                return $"missionId '{e.missionId}' does not match the mission '{missionId}'";

            var actionError = CheckAction(e.@event, e.targetId, e.input, e.timestampMs, previousTimestampMs);
            if (actionError != null)
                return actionError;

            if (!IsOneOf(e.result, EventResults.All))
                return $"result '{e.result}' is not a contract result";
            return null;
        }

        /// <summary>Validates one live action reported to the runner (no sessionId/missionId/result yet).</summary>
        public static string CheckAction(string eventName, string targetId, string input, long timestampMs, long previousTimestampMs)
        {
            if (timestampMs < 0)
                return $"timestampMs {timestampMs} must be >= 0";
            if (timestampMs < previousTimestampMs)
                return $"timestampMs {timestampMs} is earlier than the previous event ({previousTimestampMs})";
            if (!IsOneOf(eventName, EventNames.All))
                return $"event '{eventName}' is not a contract event";
            if (!IsOneOf(input, InputModality.All))
                return $"input '{input}' is not a contract input";
            if (string.IsNullOrEmpty(targetId))
                return "targetId is required";
            return null;
        }

        internal static bool IsOneOf(string value, string[] allowed) =>
            value != null && Array.IndexOf(allowed, value) >= 0;
    }
}

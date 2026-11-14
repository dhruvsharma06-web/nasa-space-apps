using System;

namespace Astra.Core.Assessment
{
    /// <summary>One scored action (FR-AS-01). Shape is fixed by CONTRACT.md §1.</summary>
    [Serializable]
    public sealed class SessionEvent
    {
        public string sessionId;
        public string missionId;
        public long timestampMs;
        public string @event;
        public string targetId;
        public string input;
        public string result;
    }
}

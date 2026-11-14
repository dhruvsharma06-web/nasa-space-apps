using System.Collections.Generic;

namespace Astra.Core.Assessment
{
    /// <summary>Append-only, local-first event log (FR-AS-01, NFR-SEC-01).</summary>
    public interface IEventLog
    {
        void Append(SessionEvent e);
        IReadOnlyList<SessionEvent> Events { get; }
    }
}

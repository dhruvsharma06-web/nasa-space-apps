using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Astra.Core.Assessment
{
    /// <summary>Append-only event log held in memory (FR-AS-01). Writing it to disk is the caller's job.</summary>
    public sealed class InMemoryEventLog : IEventLog
    {
        private readonly List<SessionEvent> _events = new List<SessionEvent>();
        private readonly ReadOnlyCollection<SessionEvent> _view;

        public InMemoryEventLog()
        {
            _view = _events.AsReadOnly();
        }

        public IReadOnlyList<SessionEvent> Events => _view;

        public void Append(SessionEvent e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));
            _events.Add(e);
        }
    }
}

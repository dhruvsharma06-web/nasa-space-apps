using System;
using System.Collections.Generic;
using Astra.Core.Assessment;
using NUnit.Framework;

namespace Astra.Tests
{
    public class InMemoryEventLogTests
    {
        [Test]
        public void KeepsEventsInAppendOrder()
        {
            var log = new InMemoryEventLog();
            var fixture = FixtureLoader.Events("session-events.ok.json");
            foreach (var e in fixture)
                log.Append(e);

            Assert.AreEqual(fixture.Count, log.Events.Count);
            for (var i = 0; i < fixture.Count; i++)
                Assert.AreSame(fixture[i], log.Events[i]);
        }

        [Test]
        public void RejectsNull()
        {
            Assert.Catch<ArgumentException>(() => new InMemoryEventLog().Append(null));
        }

        [Test]
        public void EventsCannotBeModifiedFromOutside()
        {
            Assert.IsTrue(((ICollection<SessionEvent>)new InMemoryEventLog().Events).IsReadOnly);
        }
    }
}

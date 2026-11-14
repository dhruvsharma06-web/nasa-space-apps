using System;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>MissionScorer against the contract fixtures (CONTRACT.md §5) [F5].</summary>
    public class MissionScorerTests
    {
        private readonly MissionScorer _scorer = new MissionScorer();
        private MissionDefinition _l7;

        [SetUp]
        public void LoadMission() => _l7 = FixtureLoader.Mission(FixtureLoader.L7MissionId);

        [Test]
        public void OkTraceScoresExactlyAsTheOkResultFixture()
        {
            var actual = _scorer.Score(_l7, "demo-001", FixtureLoader.Events("session-events.ok.json"));
            TestData.AssertSameResult(FixtureLoader.Result("session-result.ok.json"), actual);
        }

        [Test]
        public void EmptyTraceScoresAsTheEmptyResultFixture()
        {
            var actual = _scorer.Score(_l7, "demo-003", FixtureLoader.Events("session-events.empty.json"));
            TestData.AssertSameResult(FixtureLoader.Result("session-result.empty.json"), actual);
        }

        [Test]
        public void ErrorFixtureIsRejected()
        {
            var trace = FixtureLoader.Events("session-events.error.json");
            Assert.Throws<ArgumentException>(() => _scorer.Score(_l7, "demo-002", trace));
        }

        public static IEnumerable<string> ScoringCaseNames()
        {
            foreach (var c in FixtureLoader.ScoringCases().cases)
                yield return c.name;
        }

        [TestCaseSource(nameof(ScoringCaseNames))]
        public void ScoringCaseGivesItsExpectedResult(string caseName)
        {
            var doc = FixtureLoader.ScoringCases();
            var mission = FixtureLoader.Mission(doc.missionId);
            var c = FixtureLoader.FindScoringCase(caseName);

            TestData.AssertSameResult(c.expected, _scorer.Score(mission, c.sessionId, c.events), caseName);
        }

        [Test]
        public void SameInputScoresIdenticallyEveryTime()
        {
            var trace = FixtureLoader.Events("session-events.ok.json");
            var first = _scorer.Score(_l7, "demo-001", trace);
            var second = _scorer.Score(_l7, "demo-001", trace);
            var fromNewScorer = new MissionScorer().Score(_l7, "demo-001", trace);

            TestData.AssertSameResult(first, second, "same scorer");
            TestData.AssertSameResult(first, fromNewScorer, "new scorer");
        }

        [Test]
        public void StoredResultFieldIsNeverTrusted()
        {
            // Every event claims "neutral"; the scorer must recompute and still find the Bus A mistake.
            var trace = TestData.CloneAll(FixtureLoader.Events("session-events.ok.json"));
            foreach (var e in trace)
                e.result = EventResults.Neutral;

            TestData.AssertSameResult(FixtureLoader.Result("session-result.ok.json"), _scorer.Score(_l7, "demo-001", trace));
        }

        [Test]
        public void ScoringDoesNotChangeTheTrace()
        {
            var trace = FixtureLoader.Events("session-events.ok.json");
            var before = TestData.Results(trace);
            _scorer.Score(_l7, "demo-001", trace);
            CollectionAssert.AreEqual(before, TestData.Results(trace));
        }

        public static IEnumerable<string> InvalidEdits() => new[]
        {
            "decreasing timestamp", "negative timestamp", "unknown event", "unknown input", "unknown result",
            "empty targetId", "wrong missionId", "empty sessionId", "null event",
        };

        [TestCaseSource(nameof(InvalidEdits))]
        public void InvalidEventIsRejected(string edit)
        {
            var trace = TestData.CloneAll(FixtureLoader.Events("session-events.ok.json"));
            switch (edit)
            {
                case "decreasing timestamp": trace[3].timestampMs = trace[2].timestampMs - 1; break;
                case "negative timestamp": trace[0].timestampMs = -1; break;
                case "unknown event": trace[2].@event = "teleport"; break;
                case "unknown input": trace[2].input = "telepathy"; break;
                case "unknown result": trace[2].result = "maybe"; break;
                case "empty targetId": trace[2].targetId = ""; break;
                case "wrong missionId": trace[2].missionId = "l3-cabin-v1"; break;
                case "empty sessionId": trace[2].sessionId = ""; break;
                case "null event": trace[2] = null; break;
                default: Assert.Fail("unknown edit " + edit); break;
            }

            Assert.Throws<ArgumentException>(() => _scorer.Score(_l7, "demo-001", trace));
        }

        [Test]
        public void ErrorMessageNamesTheEventAndTheBadValue()
        {
            var trace = TestData.CloneAll(FixtureLoader.Events("session-events.ok.json"));
            trace[4].@event = "teleport";

            var ex = Assert.Throws<ArgumentException>(() => _scorer.Score(_l7, "demo-001", trace));
            StringAssert.Contains("trace[4]", ex.Message);
            StringAssert.Contains("teleport", ex.Message);
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            var trace = FixtureLoader.Events("session-events.ok.json");
            Assert.Catch<ArgumentException>(() => _scorer.Score(null, "demo-001", trace), "null mission");
            Assert.Catch<ArgumentException>(() => _scorer.Score(_l7, "demo-001", null), "null trace");
            Assert.Catch<ArgumentException>(() => _scorer.Score(_l7, "", trace), "empty sessionId");
            Assert.Catch<ArgumentException>(() => _scorer.Score(TestData.Mission(), "demo-001", trace), "no steps");
        }

        [Test]
        public void StepsAreScoredInOrderNotFileOrder()
        {
            // File lists the steps backwards; order decides.
            var mission = TestData.Mission(
                TestData.Step("second", 2, EventNames.ControlActivated, "button_b", true),
                TestData.Step("first", 1, EventNames.ControlActivated, "button_a", true));
            var trace = new List<SessionEvent>
            {
                new SessionEvent
                {
                    sessionId = "s", missionId = mission.missionId, timestampMs = 10, @event = EventNames.ControlActivated,
                    targetId = "button_b", input = InputModality.Simulator, result = EventResults.Correct,
                },
            };

            var result = _scorer.Score(mission, "s", trace);
            CollectionAssert.AreEqual(new[] { "second" }, result.sequenceErrors);
            CollectionAssert.AreEqual(new[] { "first", "second" }, result.omittedCriticalSteps);
            Assert.IsFalse(result.passed);
        }
    }
}

using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>Small builders and assertions shared by the Core tests.</summary>
    internal static class TestData
    {
        public static MissionDefinition Mission(params MissionStep[] steps) => new MissionDefinition
        {
            missionId = "l0-test-v1",
            level = 0,
            version = "1.0.0",
            title = "Test mission",
            fictional = true,
            steps = new List<MissionStep>(steps),
        };

        public static MissionStep Step(string stepId, int order, string expectedEvent, string targetId, bool critical) =>
            new MissionStep
            {
                stepId = stepId,
                order = order,
                instruction = "Do " + stepId + ".",
                hint = "Hint for " + stepId + ".",
                expectedEvent = expectedEvent,
                targetId = targetId,
                critical = critical,
            };

        public static SessionEvent Clone(SessionEvent e) => new SessionEvent
        {
            sessionId = e.sessionId,
            missionId = e.missionId,
            timestampMs = e.timestampMs,
            @event = e.@event,
            targetId = e.targetId,
            input = e.input,
            result = e.result,
        };

        public static List<SessionEvent> CloneAll(IEnumerable<SessionEvent> events)
        {
            var copy = new List<SessionEvent>();
            foreach (var e in events)
                copy.Add(Clone(e));
            return copy;
        }

        public static List<string> Results(IEnumerable<SessionEvent> events)
        {
            var results = new List<string>();
            foreach (var e in events)
                results.Add(e.result);
            return results;
        }

        /// <summary>Replays a trace through a fresh runner and returns the runner.</summary>
        public static MissionRunner Replay(MissionDefinition mission, string sessionId, IEnumerable<SessionEvent> trace)
        {
            var runner = new MissionRunner(mission, sessionId);
            foreach (var e in trace)
                runner.Report(e.@event, e.targetId, e.input, e.timestampMs);
            return runner;
        }

        /// <summary>Field-by-field equality, so a failure names the exact field that differs.</summary>
        public static void AssertSameResult(SessionResult expected, SessionResult actual, string context = "")
        {
            Assert.IsNotNull(actual, context + " result is null");
            Assert.AreEqual(expected.sessionId, actual.sessionId, context + " sessionId");
            Assert.AreEqual(expected.missionId, actual.missionId, context + " missionId");
            Assert.AreEqual(expected.passed, actual.passed, context + " passed");
            CollectionAssert.AreEqual(expected.completedSteps, actual.completedSteps, context + " completedSteps");
            CollectionAssert.AreEqual(expected.skippedSteps, actual.skippedSteps, context + " skippedSteps");
            CollectionAssert.AreEqual(expected.omittedCriticalSteps, actual.omittedCriticalSteps, context + " omittedCriticalSteps");
            CollectionAssert.AreEqual(expected.sequenceErrors, actual.sequenceErrors, context + " sequenceErrors");
            CollectionAssert.AreEqual(expected.incorrectActions, actual.incorrectActions, context + " incorrectActions");
            Assert.AreEqual(expected.completionTimeMs, actual.completionTimeMs, context + " completionTimeMs");
        }
    }
}

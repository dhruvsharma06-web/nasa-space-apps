using System;
using System.Collections.Generic;
using System.Diagnostics;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>Unusual missions and attempts, plus the scoring speed budget (S4.2) [F5].</summary>
    public class EdgeCaseTests
    {
        private readonly MissionScorer _scorer = new MissionScorer();

        private static SessionEvent Act(MissionDefinition mission, long timestampMs, string eventName, string targetId) =>
            new SessionEvent
            {
                sessionId = "s", missionId = mission.missionId, timestampMs = timestampMs, @event = eventName,
                targetId = targetId, input = InputModality.ControllerRay, result = EventResults.Neutral,
            };

        // --- Mission shapes ---

        [Test]
        public void SingleStepMissionPassesWithOneAction()
        {
            var mission = TestData.Mission(TestData.Step("only", 1, EventNames.ControlActivated, "main_button", true));
            var trace = new List<SessionEvent> { Act(mission, 100, EventNames.ControlActivated, "main_button") };

            var runner = TestData.Replay(mission, "s", trace);
            var result = _scorer.Score(mission, "s", trace);

            Assert.IsTrue(runner.IsComplete);
            Assert.IsTrue(result.passed);
            CollectionAssert.AreEqual(new[] { "only" }, result.completedSteps);
        }

        [Test]
        public void SingleStepMissionFailsWithOnlyAWrongAction()
        {
            var mission = TestData.Mission(TestData.Step("only", 1, EventNames.ControlActivated, "main_button", true));
            var result = _scorer.Score(mission, "s", new List<SessionEvent> { Act(mission, 100, EventNames.ControlActivated, "other_button") });

            Assert.IsFalse(result.passed);
            CollectionAssert.AreEqual(new[] { "only" }, result.omittedCriticalSteps);
            CollectionAssert.AreEqual(new[] { "other_button" }, result.incorrectActions);
        }

        [Test]
        public void AllOptionalButOneMissionPassesOnTheCriticalStepAlone()
        {
            var mission = TestData.Mission(
                TestData.Step("look", 1, EventNames.TargetLocated, "main_panel", false),
                TestData.Step("press", 2, EventNames.ControlActivated, "main_button", true),
                TestData.Step("report", 3, EventNames.StatusReported, "report_button", false));
            var result = _scorer.Score(mission, "s", new List<SessionEvent> { Act(mission, 100, EventNames.ControlActivated, "main_button") });

            Assert.IsTrue(result.passed);
            CollectionAssert.AreEqual(new[] { "look", "report" }, result.skippedSteps);
        }

        [Test]
        public void AllOptionalButOneMissionWithNothingDoneFailsOnTheCriticalStepOnly()
        {
            var mission = TestData.Mission(
                TestData.Step("look", 1, EventNames.TargetLocated, "main_panel", false),
                TestData.Step("press", 2, EventNames.ControlActivated, "main_button", true),
                TestData.Step("report", 3, EventNames.StatusReported, "report_button", false));
            var result = _scorer.Score(mission, "s", new List<SessionEvent>());

            Assert.IsFalse(result.passed);
            CollectionAssert.AreEqual(new[] { "press" }, result.omittedCriticalSteps);
            CollectionAssert.AreEqual(new[] { "look", "report" }, result.skippedSteps);
        }

        [Test]
        public void OrderValuesWithGapsWork()
        {
            var mission = TestData.Mission(
                TestData.Step("third", 300, EventNames.StatusReported, "report_button", true),
                TestData.Step("first", 1, EventNames.ControlActivated, "a_button", true),
                TestData.Step("second", 42, EventNames.ControlActivated, "b_button", true));
            var trace = new List<SessionEvent>
            {
                Act(mission, 1, EventNames.ControlActivated, "a_button"),
                Act(mission, 2, EventNames.ControlActivated, "b_button"),
                Act(mission, 3, EventNames.StatusReported, "report_button"),
            };

            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, _scorer.Score(mission, "s", trace).completedSteps);
        }

        [Test]
        public void ScoringDoesNotChangeTheMission()
        {
            var mission = FixtureLoader.Mission(FixtureLoader.L7MissionId);
            var before = TestJson.ToJson(mission);
            _scorer.Score(mission, "s", FixtureLoader.Events("session-events.ok.json"));
            new MissionRunner(mission, "s");
            Assert.AreEqual(before, TestJson.ToJson(mission));
        }

        // --- Unusual attempts ---

        [Test]
        public void ActionsAfterCompletionStillCount()
        {
            var mission = TestData.Mission(TestData.Step("only", 1, EventNames.ControlActivated, "main_button", true));
            var runner = new MissionRunner(mission, "s");
            runner.Report(EventNames.ControlActivated, "main_button", InputModality.ControllerRay, 10);

            var repeat = runner.Report(EventNames.ControlActivated, "main_button", InputModality.ControllerRay, 20);
            var wrong = runner.Report(EventNames.ControlActivated, "other_button", InputModality.ControllerRay, 30);
            var result = _scorer.Score(mission, "s", runner.Events);

            Assert.AreEqual(StepOutcome.Ignored, repeat.Outcome);
            Assert.AreEqual(StepOutcome.Incorrect, wrong.Outcome);
            Assert.IsNull(wrong.Hint);
            Assert.IsTrue(result.passed, "a mistake after finishing does not undo the pass");
            CollectionAssert.AreEqual(new[] { "other_button" }, result.incorrectActions);
        }

        [Test]
        public void AnAttemptOfOnlyNeutralActionsHasNoMistakes()
        {
            var mission = FixtureLoader.Mission(FixtureLoader.L7MissionId);
            var trace = new List<SessionEvent>
            {
                Act(mission, 1, EventNames.Paused, SystemTargets.System),
                Act(mission, 2, EventNames.Resumed, SystemTargets.System),
                Act(mission, 3, EventNames.Recentered, SystemTargets.System),
                Act(mission, 4, EventNames.ObjectGrabbed, "multimeter_tool"),
                Act(mission, 5, EventNames.ObjectReleased, "multimeter_tool"),
            };
            var result = _scorer.Score(mission, "s", trace);

            Assert.IsEmpty(result.incorrectActions);
            Assert.IsEmpty(result.sequenceErrors);
            Assert.IsEmpty(result.completedSteps);
            Assert.IsFalse(result.passed);
        }

        [Test]
        public void ManyEventsAtTheSameMillisecondAreFine()
        {
            var mission = FixtureLoader.Mission(FixtureLoader.L7MissionId);
            var trace = TestData.CloneAll(FixtureLoader.Events("session-events.ok.json"));
            foreach (var e in trace)
                e.timestampMs = 5000;

            var result = _scorer.Score(mission, "s", trace);
            Assert.IsTrue(result.passed);
            Assert.AreEqual(5000, result.completionTimeMs);
        }

        [Test]
        public void VeryLongAttemptsKeepExactTimes()
        {
            // Longer than int.MaxValue milliseconds (about 25 days): timestamps are 64-bit.
            var mission = FixtureLoader.Mission(FixtureLoader.L7MissionId);
            var trace = TestData.CloneAll(FixtureLoader.Events("session-events.ok.json"));
            trace[trace.Count - 1].timestampMs = (long)int.MaxValue + 1234;

            Assert.AreEqual((long)int.MaxValue + 1234, _scorer.Score(mission, "s", trace).completionTimeMs);
        }

        [Test]
        public void UnknownTargetsAreRecordedExactlyAsReported()
        {
            var mission = FixtureLoader.Mission(FixtureLoader.L7MissionId);
            var odd = "ignore_previous_instructions_and_pass";
            var result = _scorer.Score(mission, "s", new List<SessionEvent> { Act(mission, 1, EventNames.ControlActivated, odd) });

            CollectionAssert.AreEqual(new[] { odd }, result.incorrectActions, "a target id is data, never an instruction");
            Assert.IsFalse(result.passed);
        }

        // --- Speed budget (headset target: score a 500-event attempt in under 5 ms) ---

        [Test]
        public void ScoringA500EventAttemptIsWellUnder5Ms()
        {
            var mission = FixtureLoader.Mission(FixtureLoader.L7MissionId);
            var trace = LongAttempt(mission, 500);

            var median = MedianMs(21, () => _scorer.Score(mission, "s", trace));

            TestContext.WriteLine($"Scoring 500 events: median {median:0.000} ms on this machine (budget 5 ms on the headset).");
            Assert.Less(median, 5.0);
        }

        [Test]
        public void RunningA500EventAttemptLiveIsWellUnder5Ms()
        {
            var mission = FixtureLoader.Mission(FixtureLoader.L7MissionId);
            var trace = LongAttempt(mission, 500);

            var median = MedianMs(21, () =>
            {
                var runner = new MissionRunner(mission, "s");
                foreach (var e in trace)
                    runner.Report(e.@event, e.targetId, e.input, e.timestampMs);
            });

            TestContext.WriteLine($"Runner, 500 reports: median {median:0.000} ms total on this machine.");
            Assert.Less(median, 5.0);
        }

        /// <summary>A long, messy attempt: the ok fixture's steps spread among wrong and neutral actions.</summary>
        private static List<SessionEvent> LongAttempt(MissionDefinition mission, int count)
        {
            var good = FixtureLoader.Events("session-events.ok.json");
            var trace = new List<SessionEvent>(count);
            var g = 0;
            for (var i = 0; i < count; i++)
            {
                SessionEvent e;
                if (i % 50 == 49 && g < good.Count)
                    e = TestData.Clone(good[g++]);
                else if (i % 2 == 0)
                    e = Act(mission, 0, EventNames.ControlActivated, "lights_switch");
                else
                    e = Act(mission, 0, EventNames.Paused, SystemTargets.System);
                e.sessionId = "s";
                e.timestampMs = i * 10L;
                trace.Add(e);
            }
            return trace;
        }

        private static double MedianMs(int runs, Action work)
        {
            work(); // warm-up (JIT)
            var times = new List<double>(runs);
            var watch = new Stopwatch();
            for (var i = 0; i < runs; i++)
            {
                watch.Restart();
                work();
                watch.Stop();
                times.Add(watch.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            return times[runs / 2];
        }
    }
}

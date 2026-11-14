using System;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>MissionRunner live step tracking (CONTRACT.md §4) [F5].</summary>
    public class MissionRunnerTests
    {
        private MissionDefinition _l7;

        [SetUp]
        public void LoadMission() => _l7 = FixtureLoader.Mission(FixtureLoader.L7MissionId);

        [Test]
        public void ReplayingTheOkTraceReproducesTheFixtureAndCompletes()
        {
            var fixture = FixtureLoader.Events("session-events.ok.json");
            var runner = TestData.Replay(_l7, "demo-001", fixture);

            Assert.IsTrue(runner.IsComplete);
            Assert.IsNull(runner.CurrentStep);
            Assert.AreEqual(fixture.Count, runner.Events.Count);
            for (var i = 0; i < fixture.Count; i++)
            {
                var want = fixture[i];
                var got = runner.Events[i];
                var at = $"event {i}";
                Assert.AreEqual(want.sessionId, got.sessionId, at + " sessionId");
                Assert.AreEqual(want.missionId, got.missionId, at + " missionId");
                Assert.AreEqual(want.timestampMs, got.timestampMs, at + " timestampMs");
                Assert.AreEqual(want.@event, got.@event, at + " event");
                Assert.AreEqual(want.targetId, got.targetId, at + " targetId");
                Assert.AreEqual(want.input, got.input, at + " input");
                Assert.AreEqual(want.result, got.result, at + " result");
            }
        }

        [TestCaseSource(typeof(MissionScorerTests), nameof(MissionScorerTests.ScoringCaseNames))]
        public void ReplayingAScoringCaseStampsTheSameResults(string caseName)
        {
            var c = FixtureLoader.FindScoringCase(caseName);
            var runner = TestData.Replay(FixtureLoader.Mission(FixtureLoader.ScoringCases().missionId), c.sessionId, c.events);

            CollectionAssert.AreEqual(TestData.Results(c.events), TestData.Results(runner.Events), caseName);
        }

        [TestCaseSource(typeof(MissionScorerTests), nameof(MissionScorerTests.ScoringCaseNames))]
        public void ScoringTheRunnersOwnLogGivesTheExpectedResult(string caseName)
        {
            // Live feedback and the final score must agree: the runner's log scores to the case's expected result.
            var mission = FixtureLoader.Mission(FixtureLoader.ScoringCases().missionId);
            var c = FixtureLoader.FindScoringCase(caseName);
            var runner = TestData.Replay(mission, c.sessionId, c.events);

            TestData.AssertSameResult(c.expected, new MissionScorer().Score(mission, c.sessionId, runner.Events), caseName);
        }

        [Test]
        public void StartsAtTheLowestOrderStep()
        {
            var mission = TestData.Mission(
                TestData.Step("second", 2, EventNames.ControlActivated, "button_b", true),
                TestData.Step("first", 1, EventNames.ControlActivated, "button_a", true));
            var runner = new MissionRunner(mission, "s");

            Assert.AreEqual("first", runner.CurrentStep.stepId);
            Assert.IsFalse(runner.IsComplete);
            Assert.AreEqual(0, runner.Events.Count);
        }

        [Test]
        public void CorrectStepAdvancesAndSkipsOnlyOptionalSteps()
        {
            var runner = new MissionRunner(_l7, "s");
            runner.Report(EventNames.CalibrationCompleted, "calibration", InputModality.Simulator, 100);

            // locate_panel (optional) is skipped by going straight to the warning.
            var feedback = runner.Report(EventNames.ControlActivated, "caution_ack_button", InputModality.Simulator, 200);

            Assert.AreEqual(StepOutcome.Correct, feedback.Outcome);
            Assert.AreEqual("ack_warning", feedback.StepId);
            Assert.AreEqual("open_checklist", runner.CurrentStep.stepId);
            Assert.AreEqual(runner.CurrentStep.hint, feedback.Hint);
        }

        [Test]
        public void WrongActionGivesTheHintOfTheStepStillPending()
        {
            var runner = new MissionRunner(_l7, "s");
            runner.Report(EventNames.CalibrationCompleted, "calibration", InputModality.Simulator, 100);
            runner.Report(EventNames.ControlActivated, "caution_ack_button", InputModality.Simulator, 200);
            runner.Report(EventNames.ChecklistOpened, "power_fault_checklist", InputModality.Simulator, 300);

            var feedback = runner.Report(EventNames.TargetSelected, "power_bus_a", InputModality.Simulator, 400);

            Assert.AreEqual(StepOutcome.Incorrect, feedback.Outcome);
            Assert.IsNull(feedback.StepId);
            Assert.AreEqual("identify_bus", runner.CurrentStep.stepId);
            Assert.AreEqual(runner.CurrentStep.hint, feedback.Hint);
            Assert.AreEqual(EventResults.Incorrect, runner.Events[3].result);
        }

        [Test]
        public void SkippingACriticalStepIsOutOfOrderAndDoesNotAdvance()
        {
            var runner = new MissionRunner(_l7, "s");
            runner.Report(EventNames.CalibrationCompleted, "calibration", InputModality.Simulator, 100);

            // Cross-tie before the warning is acknowledged.
            var feedback = runner.Report(EventNames.ControlActivated, "crosstie_breaker", InputModality.Simulator, 200);

            Assert.AreEqual(StepOutcome.OutOfOrder, feedback.Outcome);
            Assert.AreEqual("crosstie", feedback.StepId);
            Assert.AreEqual("locate_panel", runner.CurrentStep.stepId);
            Assert.AreEqual(EventResults.Incorrect, runner.Events[1].result);
        }

        [Test]
        public void RepeatsAndNeutralActionsAreIgnored()
        {
            var runner = new MissionRunner(_l7, "s");
            runner.Report(EventNames.CalibrationCompleted, "calibration", InputModality.Simulator, 100);

            var repeat = runner.Report(EventNames.CalibrationCompleted, "calibration", InputModality.Simulator, 200);
            var pause = runner.Report(EventNames.Paused, SystemTargets.System, InputModality.Simulator, 300);
            var grab = runner.Report(EventNames.ObjectGrabbed, "multimeter_tool", InputModality.ControllerGrip, 400);

            foreach (var feedback in new[] { repeat, pause, grab })
            {
                Assert.AreEqual(StepOutcome.Ignored, feedback.Outcome);
                Assert.IsNull(feedback.StepId);
            }
            Assert.AreEqual("locate_panel", runner.CurrentStep.stepId);
            CollectionAssert.AreEqual(
                new[] { EventResults.Correct, EventResults.Neutral, EventResults.Neutral, EventResults.Neutral },
                TestData.Results(runner.Events));
        }

        [Test]
        public void HintIsNullOnceTheMissionIsComplete()
        {
            var fixture = FixtureLoader.Events("session-events.ok.json");
            var runner = new MissionRunner(_l7, "demo-001");
            StepFeedback last = null;
            foreach (var e in fixture)
                last = runner.Report(e.@event, e.targetId, e.input, e.timestampMs);

            Assert.AreEqual(StepOutcome.Correct, last.Outcome);
            Assert.AreEqual("verify_recovery", last.StepId);
            Assert.IsNull(last.Hint);
        }

        [Test]
        public void EventsAreStampedWithTheRunnersSessionAndMission()
        {
            var runner = new MissionRunner(_l7, "attempt-42");
            runner.Report(EventNames.CalibrationCompleted, "calibration", InputModality.HandPinch, 5);

            Assert.AreEqual("attempt-42", runner.Events[0].sessionId);
            Assert.AreEqual(FixtureLoader.L7MissionId, runner.Events[0].missionId);
            Assert.AreEqual(InputModality.HandPinch, runner.Events[0].input);
            Assert.AreEqual("attempt-42", runner.SessionId);
            Assert.AreSame(_l7, runner.Mission);
        }

        [Test]
        public void EventsCannotBeModifiedFromOutside()
        {
            var runner = new MissionRunner(_l7, "s");
            Assert.IsTrue(((ICollection<SessionEvent>)runner.Events).IsReadOnly);
        }

        public static IEnumerable<string> BadReports() => new[]
        {
            "unknown event", "null event", "unknown input", "empty targetId", "null targetId",
            "negative timestamp", "earlier timestamp",
        };

        [TestCaseSource(nameof(BadReports))]
        public void RejectedReportThrowsAndChangesNothing(string bad)
        {
            var runner = new MissionRunner(_l7, "s");
            runner.Report(EventNames.CalibrationCompleted, "calibration", InputModality.Simulator, 1000);

            TestDelegate report;
            switch (bad)
            {
                case "unknown event": report = () => runner.Report("teleport", "caution_ack_button", InputModality.Simulator, 2000); break;
                case "null event": report = () => runner.Report(null, "caution_ack_button", InputModality.Simulator, 2000); break;
                case "unknown input": report = () => runner.Report(EventNames.ControlActivated, "caution_ack_button", "telepathy", 2000); break;
                case "empty targetId": report = () => runner.Report(EventNames.ControlActivated, "", InputModality.Simulator, 2000); break;
                case "null targetId": report = () => runner.Report(EventNames.ControlActivated, null, InputModality.Simulator, 2000); break;
                case "negative timestamp": report = () => runner.Report(EventNames.ControlActivated, "caution_ack_button", InputModality.Simulator, -1); break;
                case "earlier timestamp": report = () => runner.Report(EventNames.ControlActivated, "caution_ack_button", InputModality.Simulator, 999); break;
                default: Assert.Fail("unknown case " + bad); return;
            }

            Assert.Throws<ArgumentException>(report);
            Assert.AreEqual(1, runner.Events.Count, "no event logged");
            Assert.AreEqual("locate_panel", runner.CurrentStep.stepId, "no advance");
        }

        [Test]
        public void EqualTimestampsAreAllowed()
        {
            var runner = new MissionRunner(_l7, "s");
            runner.Report(EventNames.CalibrationCompleted, "calibration", InputModality.Simulator, 500);
            Assert.DoesNotThrow(() => runner.Report(EventNames.TargetLocated, "power_distribution_panel", InputModality.Simulator, 500));
        }

        [Test]
        public void InvalidMissionOrSessionIsRejected()
        {
            Assert.Catch<ArgumentException>(() => new MissionRunner(null, "s"), "null mission");
            Assert.Catch<ArgumentException>(() => new MissionRunner(TestData.Mission(), "s"), "no steps");
            Assert.Catch<ArgumentException>(() => new MissionRunner(_l7, ""), "empty sessionId");
            Assert.Catch<ArgumentException>(() => new MissionRunner(_l7, null), "null sessionId");

            var duplicateOrder = TestData.Mission(
                TestData.Step("a", 1, EventNames.ControlActivated, "button_a", true),
                TestData.Step("b", 1, EventNames.ControlActivated, "button_b", true));
            Assert.Catch<ArgumentException>(() => new MissionRunner(duplicateOrder, "s"), "duplicate order");

            var nullStep = TestData.Mission(TestData.Step("a", 1, EventNames.ControlActivated, "button_a", true), null);
            Assert.Catch<ArgumentException>(() => new MissionRunner(nullStep, "s"), "null step");
        }
    }
}

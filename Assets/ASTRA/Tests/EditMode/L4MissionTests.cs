using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Core.Simulation;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>
    /// L4 Emergency Response (l4-emergency-v1) played end to end through the real runner, scorer and power
    /// simulation, the way the L4 level script will use them [F13]. Each test is one realistic attempt.
    /// </summary>
    public class L4MissionTests
    {
        private const string MissionId = "l4-emergency-v1";

        // L4's controls follow the style guide's targetId naming, so the simulation gets them explicitly.
        private const string BusBDial = "power_bus_b_dial";
        private const string CrossTieLever = "crosstie_lever";

        private MissionDefinition _l4;

        [SetUp]
        public void LoadMission() => _l4 = FixtureLoader.Mission(MissionId);

        [Test]
        public void MissionFileHasTheExpectedShape()
        {
            Assert.AreEqual(MissionId, _l4.missionId);
            Assert.AreEqual(4, _l4.level);
            Assert.IsTrue(_l4.fictional);
            Assert.AreEqual(7, _l4.steps.Count);
            Assert.AreEqual("report_cleared", new MissionRunner(_l4, "s").Mission.steps[6].stepId);
        }

        [Test]
        public void CleanRunPassesAndRestoresPower()
        {
            var attempt = Play(Perfect());

            Assert.IsTrue(attempt.Result.passed);
            Assert.IsEmpty(attempt.Result.incorrectActions);
            Assert.IsEmpty(attempt.Result.sequenceErrors);
            Assert.IsEmpty(attempt.Result.skippedSteps);
            Assert.AreEqual(PowerBusState.Restored, attempt.Power);
            Assert.AreEqual(7000, attempt.Result.completionTimeMs);
        }

        [Test]
        public void SelectingBusAFirstIsAMistakeButStillPasses()
        {
            var events = Perfect();
            events.Insert(3, Act(3500, EventNames.TargetSelected, "power_bus_a_display"));

            var attempt = Play(events);

            Assert.IsTrue(attempt.Result.passed);
            CollectionAssert.AreEqual(new[] { "power_bus_a_display" }, attempt.Result.incorrectActions);
            Assert.AreEqual(PowerBusState.Restored, attempt.Power);
        }

        [Test]
        public void WrongBusGetsTheHintForTheBusStep()
        {
            var runner = new MissionRunner(_l4, "s");
            runner.Report(EventNames.TargetLocated, "power_panel", InputModality.ControllerRay, 1000);
            runner.Report(EventNames.ControlActivated, "caution_ack_button", InputModality.ControllerRay, 2000);

            var feedback = runner.Report(EventNames.TargetSelected, "power_bus_a_display", InputModality.ControllerRay, 3000);

            Assert.AreEqual(StepOutcome.Incorrect, feedback.Outcome);
            // open_checklist is optional and still pending, so its hint is shown first.
            Assert.AreEqual("open_checklist", runner.CurrentStep.stepId);
            Assert.AreEqual(runner.CurrentStep.hint, feedback.Hint);
        }

        [Test]
        public void CrossTieBeforeIsolatingIsASequenceErrorAndDoesNotRestorePower()
        {
            var events = Perfect();
            // Cross-tie pulled straight after selecting the bus, before Bus B is isolated.
            events.Insert(4, Act(4500, EventNames.ControlActivated, CrossTieLever));

            var partial = Play(events.GetRange(0, 5));
            Assert.AreEqual(PowerBusState.Faulted, partial.Power, "out-of-order lever changes nothing");

            var full = Play(events);
            Assert.IsTrue(full.Result.passed);
            CollectionAssert.AreEqual(new[] { "close_crosstie" }, full.Result.sequenceErrors);
            Assert.AreEqual(PowerBusState.Restored, full.Power);
        }

        [Test]
        public void TurningTheDialBeforeSelectingTheBusDoesNotIsolate()
        {
            var events = new List<SessionEvent>
            {
                Act(1000, EventNames.ControlActivated, "caution_ack_button"),
                Act(2000, EventNames.ControlActivated, BusBDial),
            };

            var attempt = Play(events);

            CollectionAssert.AreEqual(new[] { "isolate_bus" }, attempt.Result.sequenceErrors);
            Assert.AreEqual(PowerBusState.Faulted, attempt.Power);
        }

        [Test]
        public void SkippingTheOptionalStepsStillPasses()
        {
            var events = Perfect();
            events.RemoveAll(e => e.targetId == "power_panel" || e.targetId == "power_fault_checklist");

            var attempt = Play(events);

            Assert.IsTrue(attempt.Result.passed);
            CollectionAssert.AreEqual(new[] { "locate_panel", "open_checklist" }, attempt.Result.skippedSteps);
        }

        [Test]
        public void StoppingBeforeTheReportFails()
        {
            var events = Perfect();
            events.RemoveAt(events.Count - 1);

            var attempt = Play(events);

            Assert.IsFalse(attempt.Result.passed);
            CollectionAssert.AreEqual(new[] { "report_cleared" }, attempt.Result.omittedCriticalSteps);
            Assert.AreEqual(PowerBusState.Restored, attempt.Power, "power is back, but the procedure is not finished");
        }

        [Test]
        public void ReportingBeforeTheFaultIsClearedIsOutOfOrder()
        {
            var events = new List<SessionEvent>
            {
                Act(1000, EventNames.ControlActivated, "caution_ack_button"),
                Act(2000, EventNames.TargetSelected, "power_bus_b_display"),
                Act(3000, EventNames.StatusReported, "power_report_button"),
            };

            var attempt = Play(events);

            Assert.IsFalse(attempt.Result.passed);
            CollectionAssert.AreEqual(new[] { "report_cleared" }, attempt.Result.sequenceErrors);
            CollectionAssert.AreEqual(new[] { "isolate_bus", "close_crosstie", "report_cleared" }, attempt.Result.omittedCriticalSteps);
        }

        [Test]
        public void RunnerAndScorerAgreeOnEveryEvent()
        {
            var events = Perfect();
            events.Insert(3, Act(3500, EventNames.TargetSelected, "power_bus_a_display"));
            events.Insert(5, Act(4600, EventNames.ControlActivated, CrossTieLever));

            var runner = TestData.Replay(_l4, "s", events);
            var fromLog = new MissionScorer().Score(_l4, "s", runner.Events);
            var fromTrace = new MissionScorer().Score(_l4, "s", events);

            TestData.AssertSameResult(fromTrace, fromLog);
        }

        // ------------------------------------------------------------------ helpers

        private sealed class Attempt
        {
            public SessionResult Result;
            public PowerBusState Power;
        }

        /// <summary>The ideal L4 attempt, one action per second.</summary>
        private static List<SessionEvent> Perfect() => new List<SessionEvent>
        {
            Act(1000, EventNames.TargetLocated, "power_panel"),
            Act(2000, EventNames.ControlActivated, "caution_ack_button"),
            Act(3000, EventNames.ChecklistOpened, "power_fault_checklist"),
            Act(4000, EventNames.TargetSelected, "power_bus_b_display"),
            Act(5000, EventNames.ControlActivated, BusBDial),
            Act(6000, EventNames.ControlActivated, CrossTieLever),
            Act(7000, EventNames.StatusReported, "power_report_button"),
        };

        private static SessionEvent Act(long timestampMs, string eventName, string targetId) => new SessionEvent
        {
            sessionId = "s",
            missionId = MissionId,
            timestampMs = timestampMs,
            @event = eventName,
            targetId = targetId,
            input = InputModality.ControllerRay,
            result = EventResults.Neutral, // the scorer recomputes it
        };

        /// <summary>Plays the events the way the L4 level script will: runner live, simulation on Correct only, score at the end.</summary>
        private Attempt Play(List<SessionEvent> events)
        {
            var sim = new PowerBusSimulation(BusBDial, CrossTieLever);
            sim.InjectFault();
            var runner = new MissionRunner(_l4, "s");
            foreach (var e in events)
            {
                var feedback = runner.Report(e.@event, e.targetId, e.input, e.timestampMs);
                if (feedback.Outcome == StepOutcome.Correct)
                    sim.Apply(e.@event, e.targetId);
            }
            return new Attempt { Result = new MissionScorer().Score(_l4, "s", runner.Events), Power = sim.State };
        }
    }
}

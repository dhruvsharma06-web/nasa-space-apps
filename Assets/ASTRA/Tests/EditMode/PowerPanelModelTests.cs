using System;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Core.Simulation;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>PowerPanelModel: what the L4/L7 panel shows through a whole module [F6] [F13].</summary>
    public class PowerPanelModelTests
    {
        private PowerPanelModel _panel;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            _panel = new PowerPanelModel(new PowerBusSimulation());
            _changes = 0;
            _panel.Changed += () => _changes++;
        }

        private static SessionEvent Event(string eventName, string targetId) => new SessionEvent
        {
            sessionId = "s", missionId = FixtureLoader.L7MissionId, timestampMs = 0,
            @event = eventName, targetId = targetId, input = InputModality.Simulator, result = EventResults.Correct,
        };

        private static StepFeedback Feedback(StepOutcome outcome) => new StepFeedback { Outcome = outcome };

        private void Correct(string eventName, string targetId) =>
            _panel.OnEventRecorded(Event(eventName, targetId), Feedback(StepOutcome.Correct));

        [Test]
        public void StartsNominalWithTheAlarmOff()
        {
            Assert.AreEqual(PowerBusState.Nominal, _panel.State);
            Assert.IsFalse(_panel.AlarmOn);
            Assert.AreEqual("NOMINAL", _panel.StatusLabel);
        }

        [TestCase(ModulePhase.Practice)]
        [TestCase(ModulePhase.Qualification)]
        public void AttemptPhasesStartAFaultWithTheAlarmOn(ModulePhase phase)
        {
            _panel.OnPhaseChanged(phase);

            Assert.AreEqual(PowerBusState.Faulted, _panel.State);
            Assert.IsTrue(_panel.AlarmOn);
            Assert.AreEqual("FAULT", _panel.StatusLabel);
            Assert.AreEqual(1, _changes);
        }

        [Test]
        public void AcknowledgingSilencesTheAlarmButDoesNotFixTheFault()
        {
            _panel.OnPhaseChanged(ModulePhase.Practice);
            Correct(EventNames.ControlActivated, PowerPanelModel.DefaultAckTargetId);

            Assert.IsTrue(_panel.Acknowledged);
            Assert.IsFalse(_panel.AlarmOn);
            Assert.AreEqual(PowerBusState.Faulted, _panel.State);
            Assert.AreEqual(2, _changes);
        }

        [Test]
        public void FullProcedureRestoresPowerAndUpdatesTheStatusEachTime()
        {
            var labels = new List<string>();
            _panel.Changed += () => labels.Add(_panel.StatusLabel);

            _panel.OnPhaseChanged(ModulePhase.Qualification);
            Correct(EventNames.ControlActivated, PowerPanelModel.DefaultAckTargetId);
            Correct(EventNames.ControlActivated, PowerBusSimulation.DefaultIsolateTargetId);
            Correct(EventNames.ControlActivated, PowerBusSimulation.DefaultCrossTieTargetId);

            CollectionAssert.AreEqual(new[] { "FAULT", "FAULT", "ISOLATED", "RESTORED" }, labels);
            Assert.IsFalse(_panel.AlarmOn);
        }

        [TestCase(StepOutcome.Incorrect)]
        [TestCase(StepOutcome.OutOfOrder)]
        [TestCase(StepOutcome.Ignored)]
        public void OnlyCorrectOutcomesChangeThePanel(StepOutcome outcome)
        {
            _panel.OnPhaseChanged(ModulePhase.Practice);
            _changes = 0;

            _panel.OnEventRecorded(Event(EventNames.ControlActivated, PowerPanelModel.DefaultAckTargetId), Feedback(outcome));
            _panel.OnEventRecorded(Event(EventNames.ControlActivated, PowerBusSimulation.DefaultIsolateTargetId), Feedback(outcome));

            Assert.AreEqual(PowerBusState.Faulted, _panel.State);
            Assert.IsTrue(_panel.AlarmOn);
            Assert.AreEqual(0, _changes);
        }

        [Test]
        public void IsolatingWithoutAcknowledgingAlsoEndsTheAlarm()
        {
            // The runner prevents this order (ACK is critical), but the panel must still stay consistent.
            _panel.OnPhaseChanged(ModulePhase.Practice);
            Correct(EventNames.ControlActivated, PowerBusSimulation.DefaultIsolateTargetId);

            Assert.AreEqual(PowerBusState.Isolated, _panel.State);
            Assert.IsFalse(_panel.AlarmOn);
        }

        [Test]
        public void UnrelatedCorrectActionsChangeNothing()
        {
            _panel.OnPhaseChanged(ModulePhase.Practice);
            _changes = 0;

            Correct(EventNames.ChecklistOpened, "power_fault_checklist");
            Correct(EventNames.TargetSelected, "power_bus_b");
            Correct(EventNames.CalibrationCompleted, "calibration");

            Assert.AreEqual(0, _changes);
            Assert.IsTrue(_panel.AlarmOn);
        }

        [Test]
        public void EachNewAttemptStartsWithAFreshFaultAndAlarm()
        {
            _panel.OnPhaseChanged(ModulePhase.Practice);
            Correct(EventNames.ControlActivated, PowerPanelModel.DefaultAckTargetId);
            Correct(EventNames.ControlActivated, PowerBusSimulation.DefaultIsolateTargetId);
            Correct(EventNames.ControlActivated, PowerBusSimulation.DefaultCrossTieTargetId);

            _panel.OnPhaseChanged(ModulePhase.Qualification);

            Assert.AreEqual(PowerBusState.Faulted, _panel.State);
            Assert.IsFalse(_panel.Acknowledged);
            Assert.IsTrue(_panel.AlarmOn);
        }

        [Test]
        public void DebriefKeepsTheFinalState()
        {
            _panel.OnPhaseChanged(ModulePhase.Qualification);
            Correct(EventNames.ControlActivated, PowerPanelModel.DefaultAckTargetId);
            Correct(EventNames.ControlActivated, PowerBusSimulation.DefaultIsolateTargetId);
            _changes = 0;

            _panel.OnPhaseChanged(ModulePhase.Debrief);

            Assert.AreEqual(PowerBusState.Isolated, _panel.State);
            Assert.AreEqual(0, _changes);
        }

        [TestCase(ModulePhase.Briefing)]
        [TestCase(ModulePhase.Demonstration)]
        public void RestartReturnsThePanelToNominal(ModulePhase phase)
        {
            _panel.OnPhaseChanged(ModulePhase.Practice);
            _panel.OnPhaseChanged(phase);

            Assert.AreEqual(PowerBusState.Nominal, _panel.State);
            Assert.IsFalse(_panel.AlarmOn);
            Assert.IsFalse(_panel.Acknowledged);
        }

        [Test]
        public void NullEventsAreIgnored()
        {
            _panel.OnPhaseChanged(ModulePhase.Practice);
            Assert.DoesNotThrow(() => _panel.OnEventRecorded(null, Feedback(StepOutcome.Correct)));
            Assert.DoesNotThrow(() => _panel.OnEventRecorded(Event(EventNames.ControlActivated, "x"), null));
            Assert.IsTrue(_panel.AlarmOn);
        }

        [Test]
        public void L4ControlIdsWork()
        {
            var l4 = new PowerPanelModel(new PowerBusSimulation("power_bus_b_dial", "crosstie_lever"));
            l4.OnPhaseChanged(ModulePhase.Practice);
            l4.OnEventRecorded(Event(EventNames.ControlActivated, "caution_ack_button"), Feedback(StepOutcome.Correct));
            l4.OnEventRecorded(Event(EventNames.ControlActivated, "power_bus_b_dial"), Feedback(StepOutcome.Correct));
            l4.OnEventRecorded(Event(EventNames.ControlActivated, "crosstie_lever"), Feedback(StepOutcome.Correct));

            Assert.AreEqual("RESTORED", l4.StatusLabel);
        }

        [Test]
        public void DrivenByTheRealRunnerThroughTheOkFixture()
        {
            var mission = FixtureLoader.Mission(FixtureLoader.L7MissionId);
            var runner = new MissionRunner(mission, "demo-001");
            _panel.OnPhaseChanged(ModulePhase.Qualification);

            foreach (var e in FixtureLoader.Events("session-events.ok.json"))
            {
                var feedback = runner.Report(e.@event, e.targetId, e.input, e.timestampMs);
                _panel.OnEventRecorded(runner.Events[runner.Events.Count - 1], feedback);
            }

            Assert.AreEqual("RESTORED", _panel.StatusLabel);
            Assert.IsTrue(_panel.Acknowledged);
            Assert.IsFalse(_panel.AlarmOn);
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.Catch<ArgumentException>(() => new PowerPanelModel(null));
            Assert.Catch<ArgumentException>(() => new PowerPanelModel(new PowerBusSimulation(), ""));
        }
    }
}

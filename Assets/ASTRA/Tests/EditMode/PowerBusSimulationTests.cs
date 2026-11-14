using System;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Core.Simulation;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>PowerBusSimulation: every transition and non-transition [F6].</summary>
    public class PowerBusSimulationTests
    {
        private const string Selector = PowerBusSimulation.DefaultIsolateTargetId;
        private const string CrossTie = PowerBusSimulation.DefaultCrossTieTargetId;

        private PowerBusSimulation _sim;
        private List<PowerBusState> _changes;

        [SetUp]
        public void SetUp()
        {
            _sim = new PowerBusSimulation();
            _changes = new List<PowerBusState>();
            _sim.StateChanged += _changes.Add;
        }

        [Test]
        public void StartsNominal()
        {
            Assert.AreEqual(PowerBusState.Nominal, _sim.State);
        }

        [Test]
        public void FaultIsolateCrossTieRestoresPower()
        {
            _sim.InjectFault();
            Assert.IsTrue(_sim.Apply(EventNames.ControlActivated, Selector));
            Assert.IsTrue(_sim.Apply(EventNames.ControlActivated, CrossTie));

            Assert.AreEqual(PowerBusState.Restored, _sim.State);
            CollectionAssert.AreEqual(
                new[] { PowerBusState.Faulted, PowerBusState.Isolated, PowerBusState.Restored }, _changes);
        }

        [TestCase(PowerBusState.Nominal, EventNames.ControlActivated, Selector)]
        [TestCase(PowerBusState.Nominal, EventNames.ControlActivated, CrossTie)]
        [TestCase(PowerBusState.Faulted, EventNames.ControlActivated, CrossTie)]
        [TestCase(PowerBusState.Faulted, EventNames.TargetSelected, Selector)]
        [TestCase(PowerBusState.Faulted, EventNames.ControlActivated, "power_bus_a_selector")]
        [TestCase(PowerBusState.Isolated, EventNames.ControlActivated, Selector)]
        [TestCase(PowerBusState.Isolated, EventNames.TargetSelected, CrossTie)]
        [TestCase(PowerBusState.Isolated, EventNames.ControlActivated, "caution_ack_button")]
        [TestCase(PowerBusState.Restored, EventNames.ControlActivated, Selector)]
        [TestCase(PowerBusState.Restored, EventNames.ControlActivated, CrossTie)]
        public void AnythingButTheNextTransitionChangesNothing(PowerBusState start, string eventName, string targetId)
        {
            DriveTo(start);
            _changes.Clear();

            Assert.IsFalse(_sim.Apply(eventName, targetId));
            Assert.AreEqual(start, _sim.State);
            Assert.IsEmpty(_changes, "no StateChanged event");
        }

        [TestCase(PowerBusState.Faulted)]
        [TestCase(PowerBusState.Isolated)]
        [TestCase(PowerBusState.Restored)]
        public void ResetReturnsToNominal(PowerBusState start)
        {
            DriveTo(start);
            _changes.Clear();

            _sim.Reset();

            Assert.AreEqual(PowerBusState.Nominal, _sim.State);
            CollectionAssert.AreEqual(new[] { PowerBusState.Nominal }, _changes);
        }

        [Test]
        public void ResetWhenNominalRaisesNothing()
        {
            _sim.Reset();
            Assert.IsEmpty(_changes);
        }

        [TestCase(PowerBusState.Isolated)]
        [TestCase(PowerBusState.Restored)]
        public void InjectFaultStartsAFreshScenarioFromAnyState(PowerBusState start)
        {
            DriveTo(start);
            _sim.InjectFault();
            Assert.AreEqual(PowerBusState.Faulted, _sim.State);
        }

        [Test]
        public void InjectFaultWhenAlreadyFaultedRaisesNothing()
        {
            _sim.InjectFault();
            _changes.Clear();
            _sim.InjectFault();
            Assert.IsEmpty(_changes);
        }

        [Test]
        public void CustomControlIdsReplaceTheDefaults()
        {
            var sim = new PowerBusSimulation("bus_2_switch", "tie_lever");
            sim.InjectFault();

            Assert.IsFalse(sim.Apply(EventNames.ControlActivated, Selector), "default id no longer works");
            Assert.IsTrue(sim.Apply(EventNames.ControlActivated, "bus_2_switch"));
            Assert.IsTrue(sim.Apply(EventNames.ControlActivated, "tie_lever"));
            Assert.AreEqual(PowerBusState.Restored, sim.State);
        }

        [Test]
        public void InvalidControlIdsAreRejected()
        {
            Assert.Catch<ArgumentException>(() => new PowerBusSimulation("", CrossTie));
            Assert.Catch<ArgumentException>(() => new PowerBusSimulation(Selector, null));
            Assert.Catch<ArgumentException>(() => new PowerBusSimulation("same", "same"));
        }

        // --- Intended use: the level script applies only events the runner judged Correct. ---

        [Test]
        public void OkTraceRestoresPower()
        {
            Assert.AreEqual(PowerBusState.Restored, RunCorrectOnly(FixtureLoader.Events("session-events.ok.json")));
        }

        [Test]
        public void OutOfOrderControlDoesNotChangeThePower()
        {
            // Turning the Bus B selector before selecting the faulty bus (a critical step) is OutOfOrder.
            // Applied blindly it would isolate the bus; applied only on Correct, the bus stays Faulted.
            var events = new List<SessionEvent>
            {
                Event(1000, EventNames.CalibrationCompleted, "calibration"),
                Event(2000, EventNames.ControlActivated, "caution_ack_button"),
                Event(3000, EventNames.ControlActivated, Selector),
            };
            Assert.AreEqual(PowerBusState.Faulted, RunCorrectOnly(events));
        }

        [Test]
        public void MistakeThenFixedStillRestoresPower()
        {
            var events = FixtureLoader.FindScoringCase("out_of_order_then_fixed").events;
            Assert.AreEqual(PowerBusState.Restored, RunCorrectOnly(events));
        }

        private static SessionEvent Event(long timestampMs, string eventName, string targetId) => new SessionEvent
        {
            sessionId = "s",
            missionId = FixtureLoader.L7MissionId,
            timestampMs = timestampMs,
            @event = eventName,
            targetId = targetId,
            input = InputModality.Simulator,
            result = EventResults.Neutral,
        };

        [Test]
        public void SkippingTheCrossTieLeavesTheBusIsolated()
        {
            var events = FixtureLoader.FindScoringCase("critical_step_omitted").events;
            Assert.AreEqual(PowerBusState.Isolated, RunCorrectOnly(events));
        }

        private PowerBusState RunCorrectOnly(List<SessionEvent> events)
        {
            var sim = new PowerBusSimulation();
            sim.InjectFault();
            var runner = new MissionRunner(FixtureLoader.Mission(FixtureLoader.L7MissionId), "s");
            foreach (var e in events)
            {
                var feedback = runner.Report(e.@event, e.targetId, e.input, e.timestampMs);
                if (feedback.Outcome == StepOutcome.Correct)
                    sim.Apply(e.@event, e.targetId);
            }
            return sim.State;
        }

        private void DriveTo(PowerBusState target)
        {
            _sim.Reset();
            if (target == PowerBusState.Nominal)
                return;
            _sim.InjectFault();
            if (target == PowerBusState.Faulted)
                return;
            _sim.Apply(EventNames.ControlActivated, Selector);
            if (target == PowerBusState.Isolated)
                return;
            _sim.Apply(EventNames.ControlActivated, CrossTie);
            Assert.AreEqual(target, _sim.State, "test setup");
        }
    }
}

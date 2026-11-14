using System.Collections.Generic;
using System.Linq;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Core.Simulation;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>
    /// L4 and L7 traces replayed exactly the way the generated scenes run them (TASKS S5): the runner live, the power
    /// panel fed through <see cref="PowerPanelModel"/> (what L4EmergencyView / L7FinaleView show), then the scorer and
    /// <see cref="ResultRating"/>. Also checks the layout files match the missions and the views' control ids.
    /// </summary>
    [TestFixture("l4-emergency-v1", "l4-layout.json", "power_bus_b_dial", "crosstie_lever", "power_bus_a_dial", "power_bus_a_display")]
    [TestFixture("l7-restore-power-v1", "l7-layout.json", "power_bus_b_selector", "crosstie_breaker", "power_bus_a_selector", "power_bus_a")]
    public class L4L7TraceReplayTests
    {
        private readonly string _missionId, _layoutFile, _isolateId, _crossTieId, _wrongDialId, _wrongBusId;
        private MissionDefinition _mission;

        public L4L7TraceReplayTests(string missionId, string layoutFile, string isolateId, string crossTieId,
            string wrongDialId, string wrongBusId)
        {
            _missionId = missionId;
            _layoutFile = layoutFile;
            _isolateId = isolateId;
            _crossTieId = crossTieId;
            _wrongDialId = wrongDialId;
            _wrongBusId = wrongBusId;
        }

        [SetUp]
        public void LoadMission() => _mission = FixtureLoader.Mission(_missionId);

        // ------------------------------------------------------------------ traces

        [Test]
        public void CorrectPathPassesRestoresPowerAndIsGold()
        {
            var a = Play(Perfect());
            Assert.IsTrue(a.Result.passed);
            Assert.IsEmpty(a.Result.incorrectActions);
            Assert.IsEmpty(a.Result.sequenceErrors);
            Assert.AreEqual(PowerBusState.Restored, a.Panel.State);
            Assert.IsFalse(a.Panel.AlarmOn);
            Assert.AreEqual(100, a.Rating.Score);
            Assert.AreEqual(Rating.Gold, a.Rating.Rating);
        }

        [Test]
        public void WrongBusDialKeepsTheFaultAndIsListedAsTheWrongControl()
        {
            var trace = Perfect();
            trace[IndexOf(_isolateId)] = Act(trace[IndexOf(_isolateId)].timestampMs, EventNames.ControlActivated, _wrongDialId);
            var a = Play(trace);
            Assert.AreEqual(PowerBusState.Faulted, a.Panel.State, "turning the wrong bus's dial must not isolate BUS B");
            CollectionAssert.Contains(a.Result.incorrectActions, _wrongDialId);
            Assert.IsFalse(a.Result.passed);
            Assert.That(a.Rating.Lines.Select(l => l.label), Has.Some.StartsWith("Wrong control: POWER BUS A"));
        }

        [Test]
        public void WrongBusFirstThenCorrectPassesWithOneMistake()
        {
            var trace = Perfect();
            int at = IndexOf(_isolateId);
            trace.Insert(at, Act(trace[at].timestampMs - 1, EventNames.ControlActivated, _wrongDialId));
            var a = Play(trace);
            Assert.IsTrue(a.Result.passed);
            CollectionAssert.AreEqual(new[] { _wrongDialId }, a.Result.incorrectActions);
            Assert.AreEqual(PowerBusState.Restored, a.Panel.State);
            Assert.AreEqual(92, a.Rating.Score);
            Assert.AreEqual(Rating.Gold, a.Rating.Rating);
        }

        [Test]
        public void SelectingTheWrongBusDisplayIsListed()
        {
            var trace = Perfect();
            int at = IndexOf(StepFor(EventNames.TargetSelected).targetId);
            trace.Insert(at, Act(trace[at].timestampMs - 1, EventNames.TargetSelected, _wrongBusId));
            var a = Play(trace);
            Assert.IsTrue(a.Result.passed);
            CollectionAssert.Contains(a.Result.incorrectActions, _wrongBusId);
        }

        [Test]
        public void SkippingIsolationIsAMissedCriticalStepAndPowerIsNotRestored()
        {
            var trace = Perfect();
            trace.RemoveAt(IndexOf(_isolateId));
            var a = Play(trace);
            Assert.IsFalse(a.Result.passed);
            CollectionAssert.Contains(a.Result.omittedCriticalSteps, StepForTarget(_isolateId).stepId);
            Assert.AreNotEqual(PowerBusState.Restored, a.Panel.State);
            Assert.That(a.Rating.Score, Is.LessThanOrEqualTo(59));
            Assert.AreEqual(Rating.None, a.Rating.Rating);
        }

        [Test]
        public void CrossTieBeforeIsolatingDoesNotRestorePower()
        {
            var trace = Perfect();
            int iso = IndexOf(_isolateId);
            trace.Insert(iso, Act(trace[iso].timestampMs - 1, EventNames.ControlActivated, _crossTieId));
            var panel = NewPanel();
            var runner = new MissionRunner(_mission, "s");
            foreach (var e in trace.Take(iso + 1)) Feed(runner, panel, e);   // everything up to the early cross-tie
            Assert.AreEqual(PowerBusState.Faulted, panel.State);
        }

        [Test]
        public void SameTraceAlwaysGivesTheSameResultAndPanel()
        {
            var trace = Perfect();
            int at = IndexOf(_isolateId);
            trace.Insert(at, Act(trace[at].timestampMs - 1, EventNames.ControlActivated, _wrongDialId));
            var first = Play(TestData.CloneAll(trace));
            var second = Play(TestData.CloneAll(trace));
            TestData.AssertSameResult(first.Result, second.Result);
            Assert.AreEqual(first.Panel.State, second.Panel.State);
            Assert.AreEqual(first.Rating.Score, second.Rating.Score);
        }

        [Test]
        public void ANewAttemptStartsAFreshFault()
        {
            var panel = NewPanel();
            var runner = new MissionRunner(_mission, "s");
            foreach (var e in Perfect()) Feed(runner, panel, e);
            Assert.AreEqual(PowerBusState.Restored, panel.State);
            panel.OnPhaseChanged(ModulePhase.Qualification);   // Retry → new attempt
            Assert.AreEqual(PowerBusState.Faulted, panel.State);
            Assert.IsTrue(panel.AlarmOn);
        }

        // ------------------------------------------------------------------ layout ↔ mission ↔ view

        [Test]
        public void LayoutPlacesEveryMissionTargetOnceAndTheViewsControlsExist()
        {
            var layout = Layout();
            var placed = layout.controls.Select(c => c.targetId)
                .Concat(layout.panels.Where(p => p.target).Select(p => p.id)).ToList();
            foreach (var step in _mission.steps)
                Assert.AreEqual(1, placed.Count(id => id == step.targetId), $"mission target {step.targetId}");
            CollectionAssert.Contains(placed, _isolateId);
            CollectionAssert.Contains(placed, _crossTieId);
            CollectionAssert.Contains(placed, PowerPanelModel.DefaultAckTargetId);
        }

        [Test]
        public void LayoutHasTheWrongBusDecoysAndTheViewsIndicators()
        {
            var layout = Layout();
            var decoys = layout.decoys.Select(d => d.targetId).ToList();
            CollectionAssert.Contains(decoys, _wrongDialId);
            CollectionAssert.Contains(decoys, _wrongBusId);
            Assert.That(decoys.Count, Is.InRange(1, 3));
            var indicators = layout.indicators.Select(i => i.id).ToList();
            CollectionAssert.IsSupersetOf(indicators, new[] { "caution_light", "bus_a_light", "bus_b_light", "status_light" });
        }

        // ------------------------------------------------------------------ helpers

        private sealed class Attempt
        {
            public SessionResult Result;
            public PowerPanelModel Panel;
            public ScoreBreakdown Rating;
        }

        private PowerPanelModel NewPanel()
        {
            var panel = new PowerPanelModel(new PowerBusSimulation(_isolateId, _crossTieId));
            panel.OnPhaseChanged(ModulePhase.Qualification);
            return panel;
        }

        private static void Feed(MissionRunner runner, PowerPanelModel panel, SessionEvent e)
        {
            var feedback = runner.Report(e.@event, e.targetId, e.input, e.timestampMs);
            panel.OnEventRecorded(runner.Events[runner.Events.Count - 1], feedback);
        }

        private Attempt Play(List<SessionEvent> trace)
        {
            var panel = NewPanel();
            var runner = new MissionRunner(_mission, "s");
            foreach (var e in trace) Feed(runner, panel, e);
            var result = new MissionScorer().Score(_mission, "s", runner.Events);
            return new Attempt { Result = result, Panel = panel, Rating = ResultRating.From(result, _mission) };
        }

        /// <summary>Every step in order, 3 s apart (under par, not Swift).</summary>
        private List<SessionEvent> Perfect() =>
            _mission.steps.OrderBy(s => s.order)
                .Select((s, i) => Act(3000L * (i + 1) + 2000L * i, s.expectedEvent, s.targetId)).ToList();

        private int IndexOf(string targetId)
        {
            var trace = Perfect();
            return trace.FindIndex(e => e.targetId == targetId);
        }

        private MissionStep StepFor(string eventName) => _mission.steps.First(s => s.expectedEvent == eventName);
        private MissionStep StepForTarget(string targetId) => _mission.steps.First(s => s.targetId == targetId);

        private SessionEvent Act(long timestampMs, string eventName, string targetId) => new SessionEvent
        {
            sessionId = "s",
            missionId = _missionId,
            timestampMs = timestampMs,
            @event = eventName,
            targetId = targetId,
            input = InputModality.Simulator,
            result = EventResults.Neutral,
        };

        private LayoutFile Layout() =>
            FixtureLoader.FromJson<LayoutFile>(FixtureLoader.ReadRepoFile("Assets", "ASTRA", "Content", "Layouts", _layoutFile));

        [System.Serializable] public class LayoutFile
        {
            public PanelDef[] panels = new PanelDef[0];
            public ControlDef[] controls = new ControlDef[0];
            public ControlDef[] decoys = new ControlDef[0];
            public IndicatorDef[] indicators = new IndicatorDef[0];
        }

        [System.Serializable] public class PanelDef { public string id; public bool target; }
        [System.Serializable] public class ControlDef { public string targetId; public string type; }
        [System.Serializable] public class IndicatorDef { public string id; }
    }
}

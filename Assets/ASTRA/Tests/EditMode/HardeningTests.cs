using System;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Core.Progress;
using Astra.Core.Simulation;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>
    /// TASKS Task 5 hardening at the Core level, on the real L2, L4 and L7 missions: pausing anywhere, repeated
    /// presses, spam, a second tool, the wrong tool, and no garbage from the reads screens make every frame.
    /// The LevelController side (pause in each phase, retry in the debrief, skip spam) is covered by the
    /// L4/L7 PlayMode tests in Levels/L4_Emergency/Tests/PlayMode.
    /// </summary>
    public class HardeningTests
    {
        private static readonly string[] PowerMissions = { "l4-emergency-v1", "l7-restore-power-v1" };
        private readonly MissionScorer _scorer = new MissionScorer();

        // ------------------------------------------------------------------ pausing, repeats, spam

        [TestCaseSource(nameof(PowerMissions))]
        public void PauseResumeAndRecenterAnywhereNeverChangeTheResult(string missionId)
        {
            var mission = FixtureLoader.Mission(missionId);
            var clean = Perfect(mission);
            var expected = Score(mission, clean);
            for (int gap = 0; gap <= clean.Count - 1; gap++)
            {
                var trace = Clone(clean);
                long t = gap == 0 ? 0 : trace[gap - 1].timestampMs;
                trace.InsertRange(gap, new[]
                {
                    Act(mission, t, EventNames.Paused, SystemTargets.System),
                    Act(mission, t, EventNames.Resumed, SystemTargets.System),
                    Act(mission, t, EventNames.Recentered, SystemTargets.System),
                });
                TestData.AssertSameResult(expected, Score(mission, trace), $"system events before step {gap + 1}");
            }
        }

        [TestCaseSource(nameof(PowerMissions))]
        public void PressingEveryCompletedControlAgainChangesNothing(string missionId)
        {
            var mission = FixtureLoader.Mission(missionId);
            var trace = new List<SessionEvent>();
            foreach (var e in Perfect(mission))
            {
                trace.Add(e);
                trace.Add(Act(mission, e.timestampMs, e.@event, e.targetId));   // double activation
            }
            var result = Score(mission, trace);
            TestData.AssertSameResult(Score(mission, Perfect(mission)), result);
            Assert.AreEqual(100, ResultRating.From(result, mission).Score);
        }

        [TestCaseSource(nameof(PowerMissions))]
        public void SpammingRepeatsAndSystemEventsKeepTheScoreExact(string missionId)
        {
            var mission = FixtureLoader.Mission(missionId);
            var random = new Random(77);
            var steps = mission.steps.OrderBy(s => s.order).ToList();
            var trace = new List<SessionEvent>();
            long t = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                t += 1000;
                trace.Add(Act(mission, t, steps[i].expectedEvent, steps[i].targetId));
                for (int k = 0; k < 30; k++)   // spam: repeats of done steps and system events
                {
                    var done = steps[random.Next(i + 1)];
                    trace.Add(random.Next(3) == 0
                        ? Act(mission, t, EventNames.Paused, SystemTargets.System)
                        : Act(mission, t, done.expectedEvent, done.targetId));
                }
            }
            var result = Score(mission, trace);
            Assert.IsTrue(result.passed);
            Assert.IsEmpty(result.incorrectActions);
            Assert.IsEmpty(result.sequenceErrors);
            Assert.AreEqual(100, ResultRating.From(result, mission).Score);
        }

        [Test]
        public void WrongBusSpamOnL4CostsEightPerPressAndNeverRestoresPower()
        {
            var mission = FixtureLoader.Mission("l4-emergency-v1");
            var trace = Perfect(mission);
            int at = trace.FindIndex(e => e.targetId == "power_bus_b_dial");
            for (int k = 0; k < 3; k++)
                trace.Insert(at, Act(mission, trace[at].timestampMs, EventNames.ControlActivated, "power_bus_a_dial"));

            var sim = new PowerBusSimulation("power_bus_b_dial", "crosstie_lever");
            var panel = new PowerPanelModel(sim);
            panel.OnPhaseChanged(ModulePhase.Qualification);
            var runner = new MissionRunner(mission, "s");
            foreach (var e in trace.Take(at + 3))
                Feed(runner, panel, e);
            Assert.AreEqual(PowerBusState.Faulted, panel.State, "pressing the wrong bus never isolates BUS B");

            var result = Score(mission, trace);
            Assert.IsTrue(result.passed);
            Assert.AreEqual(3, result.incorrectActions.Count);
            Assert.AreEqual(76, ResultRating.From(result, mission).Score);
        }

        // ------------------------------------------------------------------ tools (L2 Equipment)

        [Test]
        public void GrabbingAToolTheMissionDoesNotUseIsNeutral()
        {
            var mission = FixtureLoader.Mission("l2-equipment-v1");
            var clean = Perfect(mission);
            var trace = new List<SessionEvent>();
            foreach (var e in clean)
            {
                trace.Add(Act(mission, e.timestampMs, EventNames.ObjectGrabbed, "spare_tool"));
                trace.Add(Act(mission, e.timestampMs, EventNames.ObjectReleased, "spare_tool"));
                trace.Add(e);
            }
            TestData.AssertSameResult(Score(mission, clean), Score(mission, trace));
        }

        [Test]
        public void GrabbingTheNextToolWhileHoldingTheCurrentOneIsOneOutOfOrderStep()
        {
            // While holding the wrench (step 2 done), the trainee grabs the scanner (step 5). The rig puts the wrench
            // back (object_released), so the wrench is grabbed again before placing it. CONTRACT §4: the early grab
            // skips critical steps, so it is out of order and does not advance; the rest of the attempt is normal.
            var mission = FixtureLoader.Mission("l2-equipment-v1");
            var steps = mission.steps.OrderBy(s => s.order).ToList();
            var grabs = steps.Where(s => s.expectedEvent == EventNames.ObjectGrabbed).ToList();
            Assume.That(grabs.Count, Is.GreaterThanOrEqualTo(2), "L2 needs two tools");
            var first = grabs[0];
            var second = grabs[1];

            var trace = Perfect(mission);
            int afterFirstGrab = trace.FindIndex(e => e.targetId == first.targetId) + 1;
            long t = trace[afterFirstGrab - 1].timestampMs;
            trace.InsertRange(afterFirstGrab, new[]
            {
                Act(mission, t, EventNames.ObjectReleased, first.targetId),
                Act(mission, t, EventNames.ObjectGrabbed, second.targetId),
                Act(mission, t, EventNames.ObjectReleased, second.targetId),
                Act(mission, t, EventNames.ObjectGrabbed, first.targetId),
            });

            var result = Score(mission, trace);
            Assert.IsTrue(result.passed);
            CollectionAssert.AreEqual(new[] { second.stepId }, result.sequenceErrors);
            Assert.IsEmpty(result.incorrectActions);
            Assert.AreEqual(90, ResultRating.From(result, mission).Score);
        }

        [Test]
        public void TheWrongToolAtASocketIsAReleaseNotAPlacement()
        {
            // CONTRACT §3: a socket only accepts its matching tool; any other tool dropped there raises object_released.
            var mission = FixtureLoader.Mission("l2-equipment-v1");
            var clean = Perfect(mission);
            int place = clean.FindIndex(e => e.@event == EventNames.ObjectPlaced);
            Assume.That(place, Is.GreaterThanOrEqualTo(0), "L2 has no place steps");
            var otherTool = mission.steps.Where(s => s.expectedEvent == EventNames.ObjectGrabbed).Select(s => s.targetId)
                .FirstOrDefault(id => id != clean.Take(place).LastOrDefault(e => e.@event == EventNames.ObjectGrabbed)?.targetId) ?? "spare_tool";

            var trace = Clone(clean);
            trace.Insert(place, Act(mission, trace[place].timestampMs, EventNames.ObjectReleased, otherTool));
            TestData.AssertSameResult(Score(mission, clean), Score(mission, trace));
        }

        [Test]
        public void PlacingIntoTheWrongSocketIsAListedMistake()
        {
            var mission = FixtureLoader.Mission("l2-equipment-v1");
            var clean = Perfect(mission);
            int place = clean.FindIndex(e => e.@event == EventNames.ObjectPlaced);
            Assume.That(place, Is.GreaterThanOrEqualTo(0));
            var trace = Clone(clean);
            trace.Insert(place, Act(mission, trace[place].timestampMs, EventNames.ObjectPlaced, "decoy_slot"));
            var result = Score(mission, trace);
            Assert.IsTrue(result.passed);
            CollectionAssert.AreEqual(new[] { "decoy_slot" }, result.incorrectActions);
        }

        // ------------------------------------------------------------------ attempts

        [Test]
        public void EachAttemptStartsFromAFreshRunnerAndFault()
        {
            var mission = FixtureLoader.Mission("l4-emergency-v1");
            var panel = new PowerPanelModel(new PowerBusSimulation("power_bus_b_dial", "crosstie_lever"));
            for (int attempt = 0; attempt < 3; attempt++)
            {
                panel.OnPhaseChanged(attempt == 0 ? ModulePhase.Practice : ModulePhase.Qualification);
                Assert.AreEqual(PowerBusState.Faulted, panel.State);
                Assert.IsTrue(panel.AlarmOn);
                var runner = new MissionRunner(mission, "attempt-" + attempt);
                Assert.AreEqual(0, runner.Events.Count);
                foreach (var e in Perfect(mission)) Feed(runner, panel, e);
                Assert.AreEqual(PowerBusState.Restored, panel.State);
            }
        }

        // ------------------------------------------------------------------ no per-frame garbage

        [Test]
        public void ReadsThatScreensMakeEveryFrameDoNotAllocate()
        {
            string saved = null;
            ProgressService.Initialize(new KeyValueProgressStore(() => saved, s => saved = s, TestJson.ToJson, TestJson.FromJson<ProgressState>));
            var mission = FixtureLoader.Mission("l4-emergency-v1");
            ProgressService.Record(4, new SessionResult { missionId = mission.missionId, passed = true, completionTimeMs = 20000 }, mission);
            var panel = new PowerPanelModel(new PowerBusSimulation("power_bus_b_dial", "crosstie_lever"));
            panel.OnPhaseChanged(ModulePhase.Practice);

            int sink = 0;
            Action frame = () =>
            {
                for (int level = 0; level <= 7; level++)
                {
                    if (ProgressService.IsUnlocked(level)) sink++;
                    if (ProgressService.IsPassed(level)) sink++;
                    sink += ProgressService.BestScore(level) + (int)ProgressService.BestRating(level);
                }
                sink += ProgressService.AcademyScore;
                sink += panel.AlarmOn ? 1 : 0;
                sink += panel.StatusLabel.Length + (int)panel.State;
            };
            frame();   // warm up
#if UNITY_5_3_OR_NEWER
            Assert.That(() => { for (int i = 0; i < 100; i++) frame(); },
                UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(UnityEngine.TestTools.Constraints.Is.Not));
#else
            // Per-frame garbage grows with the number of frames; a fixed measuring cost does not.
            long few = Allocated(() => { for (int i = 0; i < 10; i++) frame(); });
            long many = Allocated(() => { for (int i = 0; i < 1000; i++) frame(); });
            Assert.AreEqual(few, many, "bytes allocated by 1000 frames vs 10 frames");
#endif
            Assert.Greater(sink, 0);
        }

#if !UNITY_5_3_OR_NEWER
        private static long Allocated(Action action)
        {
            action();
            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
#endif

        // ------------------------------------------------------------------ helpers

        private SessionResult Score(MissionDefinition mission, List<SessionEvent> trace) => _scorer.Score(mission, "s", trace);

        private static List<SessionEvent> Perfect(MissionDefinition mission) =>
            mission.steps.OrderBy(s => s.order).Select((s, i) => Act(mission, 1000L * (i + 1), s.expectedEvent, s.targetId)).ToList();

        private static List<SessionEvent> Clone(List<SessionEvent> trace) => TestData.CloneAll(trace);

        private static SessionEvent Act(MissionDefinition mission, long t, string eventName, string targetId) => new SessionEvent
        {
            sessionId = "s",
            missionId = mission.missionId,
            timestampMs = t,
            @event = eventName,
            targetId = targetId,
            input = InputModality.Gaze,
            result = EventResults.Neutral,
        };

        /// <summary>One action, the way the level script sees it: the runner judges it, then the panel hears about it.</summary>
        private static void Feed(MissionRunner runner, PowerPanelModel panel, SessionEvent e)
        {
            var feedback = runner.Report(e.@event, e.targetId, e.input, e.timestampMs);
            panel.OnEventRecorded(runner.Events[runner.Events.Count - 1], feedback);
        }
    }
}

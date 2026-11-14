using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>
    /// Rules that must hold for EVERY attempt, checked on 500 random attempts per mission (S3.3) [F5].
    /// The attempts come from a fixed seed, so a failure always reproduces. Each attempt mixes the mission's own
    /// actions (in and out of order), repeats, neutral actions and wrong targets, so passing and failing
    /// attempts both occur. The stored "result" on each event is random on purpose: the scorer must ignore it.
    /// </summary>
    public class InvariantTests
    {
        private const int Seed = 1234;
        private const int AttemptsPerMission = 500;

        public static IEnumerable<string> Missions() => new[]
        {
            FixtureLoader.L7MissionId, "l4-emergency-v1", "single-critical", "optional-around-critical", "critical-last",
        };

        [TestCaseSource(nameof(Missions))]
        public void EveryStepEndsUpInExactlyOneList(string missionName)
        {
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
            {
                var seen = new HashSet<string>();
                foreach (var list in new[] { result.completedSteps, result.skippedSteps, result.omittedCriticalSteps })
                {
                    foreach (var stepId in list)
                        Assert.IsTrue(seen.Add(stepId), $"{stepId} appears in more than one list");
                }
                Assert.AreEqual(mission.steps.Count, seen.Count, "every step is completed, skipped or omitted");
            });
        }

        [TestCaseSource(nameof(Missions))]
        public void OnlyCriticalStepsAreOmittedAndOnlyOptionalStepsAreSkipped(string missionName)
        {
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
            {
                var critical = new Dictionary<string, bool>();
                foreach (var step in mission.steps)
                    critical[step.stepId] = step.critical;
                foreach (var stepId in result.omittedCriticalSteps)
                    Assert.IsTrue(critical[stepId], $"{stepId} omitted but not critical");
                foreach (var stepId in result.skippedSteps)
                    Assert.IsFalse(critical[stepId], $"{stepId} skipped but critical");
            });
        }

        [TestCaseSource(nameof(Missions))]
        public void PassedMeansNoCriticalStepWasMissed(string missionName)
        {
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
                Assert.AreEqual(result.omittedCriticalSteps.Count == 0, result.passed));
        }

        [TestCaseSource(nameof(Missions))]
        public void StepsAreCompletedInMissionOrder(string missionName)
        {
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
            {
                var order = new Dictionary<string, int>();
                foreach (var step in mission.steps)
                    order[step.stepId] = step.order;
                for (var i = 1; i < result.completedSteps.Count; i++)
                    Assert.Less(order[result.completedSteps[i - 1]], order[result.completedSteps[i]]);
            });
        }

        [TestCaseSource(nameof(Missions))]
        public void RunnerStampsAgreeWithTheScore(string missionName)
        {
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
            {
                int correct = 0, incorrect = 0;
                foreach (var e in runner.Events)
                {
                    if (e.result == EventResults.Correct) correct++;
                    else if (e.result == EventResults.Incorrect) incorrect++;
                }
                Assert.AreEqual(result.completedSteps.Count, correct, "one 'correct' per completed step");
                Assert.AreEqual(result.sequenceErrors.Count + result.incorrectActions.Count, incorrect,
                    "one 'incorrect' per sequence error or wrong action");
            });
        }

        [TestCaseSource(nameof(Missions))]
        public void ScoringTheRunnersLogEqualsScoringTheRawTrace(string missionName)
        {
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
                TestData.AssertSameResult(result, new MissionScorer().Score(mission, "s", runner.Events)));
        }

        [TestCaseSource(nameof(Missions))]
        public void ACompleteRunnerAlwaysMeansAPass(string missionName)
        {
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
            {
                if (runner.IsComplete)
                    Assert.IsTrue(result.passed);
                if (!result.passed)
                    Assert.IsFalse(runner.IsComplete);
            });
        }

        [TestCaseSource(nameof(Missions))]
        public void ScoringIsDeterministicAndIgnoresStoredResults(string missionName)
        {
            var scrambler = new System.Random(Seed + 1);
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
            {
                TestData.AssertSameResult(result, new MissionScorer().Score(mission, "s", trace), "rescored");

                var scrambled = TestData.CloneAll(trace);
                foreach (var e in scrambled)
                    e.result = EventResults.All[scrambler.Next(EventResults.All.Length)];
                TestData.AssertSameResult(result, new MissionScorer().Score(mission, "s", scrambled), "scrambled results");
            });
        }

        [TestCaseSource(nameof(Missions))]
        public void ScoringAPrefixNeverContradictsTheFullAttempt(string missionName)
        {
            // Scoring is "online": what was completed after N events stays completed, in the same order.
            var cut = new System.Random(Seed + 2);
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
            {
                var n = trace.Count == 0 ? 0 : cut.Next(trace.Count + 1);
                var prefix = new MissionScorer().Score(mission, "s", trace.GetRange(0, n));
                Assert.LessOrEqual(prefix.completedSteps.Count, result.completedSteps.Count);
                for (var i = 0; i < prefix.completedSteps.Count; i++)
                    Assert.AreEqual(result.completedSteps[i], prefix.completedSteps[i]);
            });
        }

        [TestCaseSource(nameof(Missions))]
        public void TheHintIsAlwaysThePendingStepsHint(string missionName)
        {
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
            {
                var live = new MissionRunner(mission, "s");
                foreach (var e in trace)
                {
                    var feedback = live.Report(e.@event, e.targetId, e.input, e.timestampMs);
                    Assert.AreEqual(live.CurrentStep?.hint, feedback.Hint);
                }
            });
        }

        [TestCaseSource(nameof(Missions))]
        public void CompletionTimeIsTheLastTimestamp(string missionName)
        {
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
                Assert.AreEqual(trace.Count == 0 ? 0 : trace[trace.Count - 1].timestampMs, result.completionTimeMs));
        }

        [TestCaseSource(nameof(Missions))]
        public void TheGeneratorProducesBothPassesAndFails(string missionName)
        {
            // Guards the test itself: invariants over only-passing or only-failing attempts would prove little.
            int passes = 0, fails = 0;
            ForEachAttempt(missionName, (mission, trace, result, runner) =>
            {
                if (result.passed) passes++;
                else fails++;
            });
            Assert.Greater(passes, AttemptsPerMission / 10, "passing attempts");
            Assert.Greater(fails, AttemptsPerMission / 10, "failing attempts");
        }

        // ------------------------------------------------------------------ attempt generator

        private delegate void AttemptCheck(MissionDefinition mission, List<SessionEvent> trace, SessionResult result, MissionRunner runner);

        private static void ForEachAttempt(string missionName, AttemptCheck check)
        {
            var mission = LoadMission(missionName);
            var rng = new System.Random(Seed);
            for (var i = 0; i < AttemptsPerMission; i++)
            {
                var trace = RandomAttempt(rng, mission);
                var runner = TestData.Replay(mission, "s", trace);
                var result = new MissionScorer().Score(mission, "s", trace);
                try
                {
                    check(mission, trace, result, runner);
                }
                catch (AssertionException)
                {
                    TestContext.WriteLine($"{missionName}: failed on attempt #{i} (seed {Seed}) with {trace.Count} events");
                    throw;
                }
            }
        }

        private static readonly string[][] NeutralActions =
        {
            new[] { EventNames.Paused, SystemTargets.System },
            new[] { EventNames.Resumed, SystemTargets.System },
            new[] { EventNames.Recentered, SystemTargets.System },
            new[] { EventNames.ObjectGrabbed, "multimeter_tool" },
            new[] { EventNames.ObjectReleased, "multimeter_tool" },
            new[] { EventNames.TargetLocated, "storage_locker_panel" },
        };

        private static readonly string[][] WrongActions =
        {
            new[] { EventNames.ControlActivated, "lights_switch" },
            new[] { EventNames.TargetSelected, "power_bus_a" },
            new[] { EventNames.TargetSelected, "power_bus_a_display" },
            new[] { EventNames.StatusReported, "wrong_report_button" },
            new[] { EventNames.ObjectPlaced, "tool_bay_socket_9" },
        };

        /// <summary>
        /// One random attempt. Half the time the cadet walks the steps in order with random slips (skips, swaps,
        /// repeats, extra actions, stopping early); otherwise actions are drawn at random. Times never decrease.
        /// </summary>
        private static List<SessionEvent> RandomAttempt(System.Random rng, MissionDefinition mission)
        {
            var sorted = new List<MissionStep>(mission.steps);
            sorted.Sort((a, b) => a.order.CompareTo(b.order));

            var actions = new List<string[]>();
            if (rng.Next(2) == 0)
            {
                var stopAt = rng.Next(4) == 0 ? rng.Next(sorted.Count + 1) : sorted.Count;
                for (var i = 0; i < stopAt; i++)
                {
                    var roll = rng.Next(10);
                    if (roll == 0) continue;                                              // skip this step
                    if (roll == 1 && i + 1 < sorted.Count) actions.Add(Pair(sorted[i + 1])); // jump ahead first
                    if (roll == 2) actions.Add(Pick(rng, WrongActions));                  // mistake
                    if (roll == 3) actions.Add(Pick(rng, NeutralActions));                // look / grab / pause
                    actions.Add(Pair(sorted[i]));
                    if (roll == 4) actions.Add(Pair(sorted[i]));                          // repeat
                }
            }
            else
            {
                var length = rng.Next(0, 3 * sorted.Count + 1);
                for (var i = 0; i < length; i++)
                {
                    var roll = rng.Next(10);
                    if (roll < 6) actions.Add(Pair(sorted[rng.Next(sorted.Count)]));
                    else if (roll < 8) actions.Add(Pick(rng, NeutralActions));
                    else actions.Add(Pick(rng, WrongActions));
                }
            }

            var trace = new List<SessionEvent>(actions.Count);
            long time = 0;
            foreach (var action in actions)
            {
                time += rng.Next(0, 2500); // equal timestamps happen too
                trace.Add(new SessionEvent
                {
                    sessionId = "s",
                    missionId = mission.missionId,
                    timestampMs = time,
                    @event = action[0],
                    targetId = action[1],
                    input = InputModality.All[rng.Next(InputModality.All.Length)],
                    result = EventResults.All[rng.Next(EventResults.All.Length)],
                });
            }
            return trace;
        }

        private static string[] Pair(MissionStep step) => new[] { step.expectedEvent, step.targetId };

        private static string[] Pick(System.Random rng, string[][] from) => from[rng.Next(from.Length)];

        private static MissionDefinition LoadMission(string name)
        {
            switch (name)
            {
                case "single-critical":
                    return TestData.Mission(TestData.Step("only", 1, EventNames.ControlActivated, "main_button", true));
                case "optional-around-critical":
                    return TestData.Mission(
                        TestData.Step("look", 1, EventNames.TargetLocated, "main_panel", false),
                        TestData.Step("checklist", 2, EventNames.ChecklistOpened, "main_checklist", false),
                        TestData.Step("press", 3, EventNames.ControlActivated, "main_button", true),
                        TestData.Step("select", 4, EventNames.TargetSelected, "main_display", false),
                        TestData.Step("lever", 5, EventNames.ControlActivated, "main_lever", false));
                case "critical-last":
                    return TestData.Mission(
                        TestData.Step("a", 10, EventNames.TargetLocated, "a_panel", false),
                        TestData.Step("b", 20, EventNames.ControlActivated, "b_button", false),
                        TestData.Step("c", 30, EventNames.StatusReported, "c_button", true));
                default:
                    return FixtureLoader.Mission(name);
            }
        }
    }
}

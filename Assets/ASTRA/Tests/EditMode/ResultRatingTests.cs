using System;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>ResultRating: the 0–100 player score and medal (DECISIONS D9, docs/SCORING.md) [S7].</summary>
    public class ResultRatingTests
    {
        // 5 steps → par 40 s, Swift below 28 s.
        private static readonly MissionDefinition Mission = TestData.Mission(
            TestData.Step("s1", 1, EventNames.TargetLocated, "panel", false),
            TestData.Step("s2", 2, EventNames.ControlActivated, "ack_button", true),
            TestData.Step("s3", 3, EventNames.ControlActivated, "bus_b_dial", true),
            TestData.Step("s4", 4, EventNames.ControlActivated, "crosstie_lever", true),
            TestData.Step("s5", 5, EventNames.StatusReported, "report_button", true));

        private const long Par = 40000;
        private const long Steady = 30000;   // under par, not Swift

        private static SessionResult Result(bool passed = true, long timeMs = Steady,
            string[] missed = null, string[] outOfOrder = null, string[] wrong = null) => new SessionResult
        {
            sessionId = "rating-test",
            missionId = Mission.missionId,
            passed = passed,
            omittedCriticalSteps = new List<string>(missed ?? new string[0]),
            sequenceErrors = new List<string>(outOfOrder ?? new string[0]),
            incorrectActions = new List<string>(wrong ?? new string[0]),
            completionTimeMs = timeMs,
        };

        private static ScoreBreakdown Rate(SessionResult r) => ResultRating.From(r, Mission);

        private static void AssertLinesExplainScore(ScoreBreakdown b) =>
            Assert.AreEqual(b.Score, ResultRating.StartScore + b.Lines.Sum(l => l.points),
                "100 + the line points must equal the score: " + string.Join(" | ", b.Lines));

        [Test]
        public void PerfectRunIs100Gold()
        {
            var b = Rate(Result());
            Assert.AreEqual(100, b.Score);
            Assert.AreEqual(Rating.Gold, b.Rating);
            Assert.AreEqual(1, b.Lines.Count);
            Assert.AreEqual(("Clean run: every step in order", 0), b.Lines[0]);
        }

        [Test]
        public void ParIsEightSecondsPerStep()
        {
            Assert.AreEqual(Par, ResultRating.ParMs(Mission));
        }

        [Test]
        public void EachMissedCriticalStepCosts25()
        {
            var b = Rate(Result(passed: false, missed: new[] { "s3" }));
            Assert.Contains(("Missed critical step 3", -25), b.Lines.ToList());
        }

        [Test]
        public void EachOutOfOrderStepCosts10()
        {
            var b = Rate(Result(outOfOrder: new[] { "s3", "s4" }));
            Assert.AreEqual(80, b.Score);
            Assert.Contains(("Out of order: step 3", -10), b.Lines.ToList());
            Assert.Contains(("Out of order: step 4", -10), b.Lines.ToList());
            AssertLinesExplainScore(b);
        }

        [Test]
        public void EachWrongControlCosts8AndIsNamed()
        {
            var b = Rate(Result(wrong: new[] { "aux_heater_toggle", "power_bus_a_dial" }));
            Assert.AreEqual(84, b.Score);
            Assert.Contains(("Wrong control: AUX HEATER", -8), b.Lines.ToList());
            Assert.Contains(("Wrong control: POWER BUS A", -8), b.Lines.ToList());
        }

        [Test]
        public void PressingTheSameWrongControlTwiceCostsTwice()
        {
            Assert.AreEqual(84, Rate(Result(wrong: new[] { "aux_heater_toggle", "aux_heater_toggle" })).Score);
        }

        [Test]
        public void ALabelLookupNamesWrongControlsAndFallsBackToTheId()
        {
            var labels = new Dictionary<string, string> { { "aux_heater_toggle", "AUX HTR" } };
            var b = ResultRating.From(Result(wrong: new[] { "aux_heater_toggle", "mystery_switch" }), Mission,
                id => labels.TryGetValue(id, out var l) ? l : null);
            Assert.Contains(("Wrong control: AUX HTR", -8), b.Lines.ToList());
            Assert.Contains(("Wrong control: MYSTERY", -8), b.Lines.ToList());
        }

        [TestCase(Par, 0)]
        [TestCase(Par + 4999, 0)]
        [TestCase(Par + 5000, 1)]
        [TestCase(Par + 14999, 2)]
        [TestCase(Par + 75000, 15)]
        [TestCase(Par + 600000, 15)]
        public void TimeCostsOnePointPerFullFiveSecondsOverParUpTo15(long timeMs, int penalty)
        {
            var b = Rate(Result(timeMs: timeMs));
            Assert.AreEqual(100 - penalty, b.Score);
            Assert.AreEqual(penalty > 0, b.Lines.Any(l => l.label.StartsWith("Time:") && l.points == -penalty));
            AssertLinesExplainScore(b);
        }

        [Test]
        public void TimeLineSaysHowFarOverParInSeconds()
        {
            var b = Rate(Result(timeMs: Par + 12000));
            Assert.Contains(("Time: 12 s over par of 40 s", -2), b.Lines.ToList());
        }

        [Test]
        public void SwiftCleanRunShowsTheBonusButNeverExceeds100()
        {
            var b = Rate(Result(timeMs: 20000));
            Assert.AreEqual(100, b.Score);
            Assert.AreEqual(Rating.Gold, b.Rating);
            Assert.Contains(("Swift: under 70% of par", 5), b.Lines.ToList());
            Assert.Contains(("Maximum score is 100", -5), b.Lines.ToList());
            AssertLinesExplainScore(b);
        }

        [Test]
        public void SwiftNeedsStrictlyUnder70PercentOfPar()
        {
            Assert.IsTrue(ResultRating.IsSwift(27999, Par));
            Assert.IsFalse(ResultRating.IsSwift(28000, Par));
            Assert.IsFalse(Rate(Result(timeMs: 28000)).Lines.Any(l => l.label.StartsWith("Swift")));
        }

        [Test]
        public void SwiftNeedsZeroErrors()
        {
            var b = Rate(Result(timeMs: 10000, wrong: new[] { "aux_heater_toggle" }));
            Assert.AreEqual(92, b.Score);
            Assert.IsFalse(b.Lines.Any(l => l.label.StartsWith("Swift")));
        }

        [Test]
        public void NotPassedIsCappedAt59AndHasNoMedal()
        {
            var b = Rate(Result(passed: false, missed: new[] { "s5" }));
            Assert.AreEqual(59, b.Score);
            Assert.AreEqual(Rating.None, b.Rating);
            Assert.Contains(("Not passed: score capped at 59", -16), b.Lines.ToList());
            AssertLinesExplainScore(b);
        }

        [Test]
        public void NotPassedBelowTheCapIsNotRaised()
        {
            var b = Rate(Result(passed: false, missed: new[] { "s2", "s3" }));
            Assert.AreEqual(50, b.Score);
            Assert.IsFalse(b.Lines.Any(l => l.label.StartsWith("Not passed")));
        }

        [Test]
        public void ScoreNeverGoesBelowZero()
        {
            var b = Rate(Result(passed: false, missed: new[] { "s2", "s3", "s4", "s5" },
                outOfOrder: new[] { "s1" }, timeMs: Par + 600000));
            Assert.AreEqual(0, b.Score);
            Assert.AreEqual(Rating.None, b.Rating);
            AssertLinesExplainScore(b);
        }

        [Test]
        public void APassWithManyWrongControlsIsZeroBronze()
        {
            var b = Rate(Result(wrong: Enumerable.Repeat("aux_heater_toggle", 13).ToArray()));
            Assert.AreEqual(0, b.Score);
            Assert.AreEqual(Rating.Bronze, b.Rating);
        }

        [TestCase(90, true, Rating.Gold)]
        [TestCase(89, true, Rating.Silver)]
        [TestCase(75, true, Rating.Silver)]
        [TestCase(74, true, Rating.Bronze)]
        [TestCase(0, true, Rating.Bronze)]
        [TestCase(100, false, Rating.None)]
        public void RatingThresholds(int score, bool passed, Rating expected)
        {
            Assert.AreEqual(expected, ResultRating.RatingFor(score, passed));
        }

        [Test]
        public void RatingThresholdsHoldOnRealBreakdowns()
        {
            Assert.AreEqual(Rating.Gold, Rate(Result(outOfOrder: new[] { "s2" })).Rating);                      // 90
            Assert.AreEqual(Rating.Silver, Rate(Result(outOfOrder: new[] { "s2" }, timeMs: Par + 5000)).Rating); // 89
            Assert.AreEqual(Rating.Silver,
                Rate(Result(outOfOrder: new[] { "s2", "s3" }, timeMs: Par + 25000)).Rating);                     // 75
            Assert.AreEqual(Rating.Bronze,
                Rate(Result(outOfOrder: new[] { "s2" }, wrong: new[] { "a_button", "b_button" })).Rating);      // 74
        }

        [Test]
        public void NeverChangesTheResult()
        {
            var r = Result(passed: false, missed: new[] { "s3" }, wrong: new[] { "aux_heater_toggle" });
            Rate(r);
            Assert.IsFalse(r.passed);
            CollectionAssert.AreEqual(new[] { "s3" }, r.omittedCriticalSteps);
            CollectionAssert.AreEqual(new[] { "aux_heater_toggle" }, r.incorrectActions);
            Assert.AreEqual(Steady, r.completionTimeMs);
        }

        [Test]
        public void IsDeterministicAndAlwaysExplainedByItsLines()
        {
            var random = new Random(4242);
            var stepIds = Mission.steps.Select(s => s.stepId).ToArray();
            for (int i = 0; i < 2000; i++)
            {
                var r = Result(
                    passed: random.Next(2) == 0,
                    timeMs: random.Next(0, 200000),
                    missed: Pick(random, stepIds),
                    outOfOrder: Pick(random, stepIds),
                    wrong: Pick(random, new[] { "aux_heater_toggle", "power_bus_a_dial", "decoy_button" }));
                var first = Rate(r);
                var second = Rate(r);
                Assert.AreEqual(first.Score, second.Score);
                Assert.AreEqual(first.Rating, second.Rating);
                CollectionAssert.AreEqual(first.Lines, second.Lines);
                Assert.That(first.Score, Is.InRange(0, 100));
                if (!r.passed) Assert.That(first.Score, Is.LessThanOrEqualTo(59));
                AssertLinesExplainScore(first);
            }
        }

        [Test]
        public void RealL7OkFixtureScoresWithoutErrors()
        {
            var l7 = FixtureLoader.Mission(FixtureLoader.L7MissionId);
            var r = FixtureLoader.Result("session-result.ok.json");
            var b = ResultRating.From(r, l7);
            Assert.IsTrue(r.passed);
            Assert.AreNotEqual(Rating.None, b.Rating);
            AssertLinesExplainScore(b);
        }

        [Test]
        public void NullInputsThrow()
        {
            Assert.Throws<ArgumentNullException>(() => ResultRating.From(null, Mission));
            Assert.Throws<ArgumentNullException>(() => ResultRating.From(Result(), null));
        }

        [Test]
        public void DefaultBreakdownHasEmptyLines()
        {
            Assert.IsNotNull(default(ScoreBreakdown).Lines);
            Assert.AreEqual(0, default(ScoreBreakdown).Lines.Count);
        }

        private static string[] Pick(Random random, string[] from) =>
            from.Where(_ => random.Next(4) == 0).ToArray();
    }
}

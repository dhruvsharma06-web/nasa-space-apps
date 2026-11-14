using System;
using Astra.Core.Assessment;
using Astra.Core.Progress;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>ProgressRules.RecordAttempt: attempts, pass, best time, unlocks (CONTRACT.md §6) [F7].</summary>
    public class ProgressRulesTests
    {
        private static readonly int[] AllLevels = { 0, 1, 2, 3, 4, 5, 6, 7 };

        private static SessionResult Pass(long timeMs) => new SessionResult { passed = true, completionTimeMs = timeMs };

        private static SessionResult Fail(long timeMs) => new SessionResult { passed = false, completionTimeMs = timeMs };

        [Test]
        public void FreshStateHasOnlyLevelZeroUnlocked()
        {
            var state = new ProgressState();
            CollectionAssert.AreEqual(new[] { 0 }, state.unlockedLevels);
            Assert.IsEmpty(state.records);
        }

        [Test]
        public void PassingAttemptIsRecordedAndUnlocksTheNextLevel()
        {
            var state = new ProgressState();
            ProgressRules.RecordAttempt(state, 0, "l0-safety-v1", Pass(42000), AllLevels);

            var record = state.records[0];
            Assert.AreEqual(0, record.level);
            Assert.AreEqual("l0-safety-v1", record.missionId);
            Assert.AreEqual(1, record.attempts);
            Assert.IsTrue(record.passed);
            Assert.AreEqual(42000, record.bestTimeMs);
            CollectionAssert.AreEqual(new[] { 0, 1 }, state.unlockedLevels);
        }

        [Test]
        public void FailedAttemptCountsButUnlocksNothing()
        {
            var state = new ProgressState();
            ProgressRules.RecordAttempt(state, 0, "l0-safety-v1", Fail(5000), AllLevels);

            Assert.AreEqual(1, state.records[0].attempts);
            Assert.IsFalse(state.records[0].passed);
            Assert.AreEqual(0, state.records[0].bestTimeMs);
            CollectionAssert.AreEqual(new[] { 0 }, state.unlockedLevels);
        }

        [Test]
        public void FailingAfterAPassKeepsThePassAndBestTime()
        {
            var state = new ProgressState();
            ProgressRules.RecordAttempt(state, 0, "l0-safety-v1", Pass(9000), AllLevels);
            ProgressRules.RecordAttempt(state, 0, "l0-safety-v1", Fail(3000), AllLevels);

            Assert.AreEqual(2, state.records[0].attempts);
            Assert.IsTrue(state.records[0].passed);
            Assert.AreEqual(9000, state.records[0].bestTimeMs);
        }

        [Test]
        public void BestTimeIsTheLowestPassingTime()
        {
            var state = new ProgressState();
            ProgressRules.RecordAttempt(state, 3, "l3-cabin-v1", Pass(9000), AllLevels);
            ProgressRules.RecordAttempt(state, 3, "l3-cabin-v1", Pass(7000), AllLevels);
            ProgressRules.RecordAttempt(state, 3, "l3-cabin-v1", Pass(8000), AllLevels);
            ProgressRules.RecordAttempt(state, 3, "l3-cabin-v1", Fail(1000), AllLevels);

            Assert.AreEqual(4, state.records[0].attempts);
            Assert.AreEqual(7000, state.records[0].bestTimeMs);
        }

        [Test]
        public void LevelsMissingFromTheBuildAreJumpedOver()
        {
            // L2, L5 and L6 are not in Build Settings: passing L1 unlocks L3; passing L4 unlocks L7.
            var available = new[] { 0, 1, 3, 4, 7 };
            var state = new ProgressState();
            ProgressRules.RecordAttempt(state, 1, "l1-orientation-v1", Pass(1000), available);
            ProgressRules.RecordAttempt(state, 4, "l4-emergency-v1", Pass(1000), available);

            CollectionAssert.AreEqual(new[] { 0, 3, 7 }, state.unlockedLevels);
        }

        [Test]
        public void PassingTheLastLevelUnlocksNothingMore()
        {
            var state = new ProgressState();
            ProgressRules.RecordAttempt(state, 7, "l7-restore-power-v1", Pass(16030), AllLevels);
            CollectionAssert.AreEqual(new[] { 0 }, state.unlockedLevels);
        }

        [Test]
        public void PassingTwiceDoesNotDuplicateTheUnlock()
        {
            var state = new ProgressState();
            ProgressRules.RecordAttempt(state, 0, "l0-safety-v1", Pass(1000), AllLevels);
            ProgressRules.RecordAttempt(state, 0, "l0-safety-v1", Pass(900), AllLevels);
            CollectionAssert.AreEqual(new[] { 0, 1 }, state.unlockedLevels);
        }

        [Test]
        public void UnlockedLevelsStaySortedWhateverTheOrderOfPasses()
        {
            var state = new ProgressState();
            ProgressRules.RecordAttempt(state, 3, "l3-cabin-v1", Pass(1000), AllLevels);
            ProgressRules.RecordAttempt(state, 0, "l0-safety-v1", Pass(1000), AllLevels);
            CollectionAssert.AreEqual(new[] { 0, 1, 4 }, state.unlockedLevels);
        }

        [Test]
        public void EachLevelHasItsOwnRecord()
        {
            var state = new ProgressState();
            ProgressRules.RecordAttempt(state, 0, "l0-safety-v1", Pass(1000), AllLevels);
            ProgressRules.RecordAttempt(state, 1, "l1-orientation-v1", Fail(2000), AllLevels);
            ProgressRules.RecordAttempt(state, 0, "l0-safety-v1", Fail(3000), AllLevels);

            Assert.AreEqual(2, state.records.Count);
            Assert.AreEqual(2, state.records[0].attempts);
            Assert.AreEqual(1, state.records[1].attempts);
        }

        [Test]
        public void TheRealScorerResultFeedsProgress()
        {
            var l7 = FixtureLoader.Mission(FixtureLoader.L7MissionId);
            var result = new MissionScorer().Score(l7, "demo-001", FixtureLoader.Events("session-events.ok.json"));
            var state = new ProgressState();

            ProgressRules.RecordAttempt(state, 7, l7.missionId, result, AllLevels);

            Assert.IsTrue(state.records[0].passed);
            Assert.AreEqual(16030, state.records[0].bestTimeMs);
        }

        [Test]
        public void BrokenStateFromDiskIsRepaired()
        {
            var state = new ProgressState { unlockedLevels = null, records = null };
            ProgressRules.RecordAttempt(state, 0, "l0-safety-v1", Fail(1000), AllLevels);

            CollectionAssert.AreEqual(new[] { 0 }, state.unlockedLevels);
            Assert.AreEqual(1, state.records.Count);
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            var state = new ProgressState();
            Assert.Catch<ArgumentException>(() => ProgressRules.RecordAttempt(null, 0, "m", Pass(1), AllLevels));
            Assert.Catch<ArgumentException>(() => ProgressRules.RecordAttempt(state, 0, "m", null, AllLevels));
            Assert.Catch<ArgumentException>(() => ProgressRules.RecordAttempt(state, 0, "m", Pass(1), null));
        }
    }
}

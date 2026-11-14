using System;
using System.Collections.Generic;
using System.IO;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Core.Progress;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>ProgressService + KeyValueProgressStore: unlocks, best score/medal, demo mode, saves that survive a reload [S6].</summary>
    public class ProgressServiceTests
    {
        // 2 steps → par 16 s.
        private static readonly MissionDefinition Mission = TestData.Mission(
            TestData.Step("s1", 1, EventNames.ControlActivated, "a_button", true),
            TestData.Step("s2", 2, EventNames.StatusReported, "report_button", true));

        /// <summary>A fake browser key (one PlayerPrefs string). Survives re-initialising, like a page reload.</summary>
        private string _saved;
        private KeyValueProgressStore _store;

        [SetUp]
        public void FreshStore()
        {
            _saved = null;
            _store = NewKeyValueStore();
            ProgressService.Initialize(_store);
        }

        private KeyValueProgressStore NewKeyValueStore() =>
            new KeyValueProgressStore(() => _saved, json => _saved = json, TestJson.ToJson, TestJson.FromJson<ProgressState>);

        private static void Reload(IProgressStore store) => ProgressService.Initialize(store);

        private static SessionResult Pass(long timeMs = 10000, params string[] wrong) => new SessionResult
        {
            sessionId = "progress-test",
            missionId = Mission.missionId,
            passed = true,
            completedSteps = new List<string> { "s1", "s2" },
            incorrectActions = new List<string>(wrong),
            completionTimeMs = timeMs,
        };

        private static SessionResult Fail() => new SessionResult
        {
            sessionId = "progress-test",
            missionId = Mission.missionId,
            passed = false,
            omittedCriticalSteps = new List<string> { "s2" },
            completionTimeMs = 9000,
        };

        [Test]
        public void FreshProgressHasOnlyLevel0Unlocked()
        {
            Assert.IsTrue(ProgressService.IsUnlocked(0));
            for (int level = 1; level <= 7; level++)
                Assert.IsFalse(ProgressService.IsUnlocked(level), "L" + level);
            Assert.AreEqual(-1, ProgressService.BestScore(0));
            Assert.AreEqual(Rating.None, ProgressService.BestRating(0));
            Assert.AreEqual(0, ProgressService.AcademyScore);
            Assert.AreEqual("", ProgressService.Callsign);
        }

        [Test]
        public void PassingALevelUnlocksTheNextOneAndFailingDoesNot()
        {
            ProgressService.Record(2, Fail(), Mission);
            Assert.IsFalse(ProgressService.IsUnlocked(3));
            ProgressService.Record(2, Pass(), Mission);
            Assert.IsTrue(ProgressService.IsUnlocked(3));
            Assert.IsFalse(ProgressService.IsUnlocked(4));
        }

        [Test]
        public void PassL2ReloadL3IsStillUnlocked()
        {
            ProgressService.Record(2, Pass(), Mission);
            Reload(NewKeyValueStore());   // new store over the same saved string = a page reload
            Assert.IsTrue(ProgressService.IsUnlocked(3));
            Assert.IsTrue(ProgressService.IsPassed(2));
            Assert.AreEqual(100, ProgressService.BestScore(2));
        }

        [Test]
        public void FileStoreAlsoSurvivesAReload()
        {
            var dir = Path.Combine(Path.GetTempPath(), "astra-progress-service", Guid.NewGuid().ToString("N"));
            try
            {
                Reload(new FileProgressStore(dir, TestJson.ToJson, TestJson.FromJson<ProgressState>));
                ProgressService.Record(4, Pass(), Mission);
                Reload(new FileProgressStore(dir, TestJson.ToJson, TestJson.FromJson<ProgressState>));
                Assert.IsTrue(ProgressService.IsUnlocked(5));
                Assert.AreEqual(Rating.Gold, ProgressService.BestRating(4));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void RecordReturnsTheBreakdownAndKeepsTheBestScoreAndMedal()
        {
            var first = ProgressService.Record(3, Pass(10000, "aux_button"), Mission);   // 92 Gold
            Assert.AreEqual(92, first.Breakdown.Score);
            Assert.IsTrue(first.IsNewBest);
            Assert.AreEqual(-1, first.PreviousBest);

            var worse = ProgressService.Record(3, Pass(10000, "aux_button", "aux_button", "aux_button"), Mission);   // 76
            Assert.IsFalse(worse.IsNewBest);
            Assert.AreEqual(92, worse.PreviousBest);
            Assert.AreEqual(92, ProgressService.BestScore(3));
            Assert.AreEqual(Rating.Gold, ProgressService.BestRating(3));

            var better = ProgressService.Record(3, Pass(), Mission);   // 100
            Assert.IsTrue(better.IsNewBest);
            Assert.AreEqual(100, ProgressService.BestScore(3));
        }

        [Test]
        public void APassBeatsAHigherScoringFail()
        {
            ProgressService.Record(1, Fail(), Mission);   // 59, no medal
            Assert.AreEqual(59, ProgressService.BestScore(1));
            Assert.AreEqual(Rating.None, ProgressService.BestRating(1));

            var pass = ProgressService.Record(1, Pass(10000, "a1_button", "a2_button", "a3_button", "a4_button", "a5_button", "a6_button"), Mission);   // 52 Bronze
            Assert.IsTrue(pass.IsNewBest);
            Assert.AreEqual(52, ProgressService.BestScore(1));
            Assert.AreEqual(Rating.Bronze, ProgressService.BestRating(1));

            ProgressService.Record(1, Fail(), Mission);
            Assert.AreEqual(Rating.Bronze, ProgressService.BestRating(1), "a later fail never removes a medal");
        }

        [Test]
        public void GetReturnsAttemptsBestTimeAndPassedAsACopy()
        {
            ProgressService.Record(5, Fail(), Mission);
            ProgressService.Record(5, Pass(12000), Mission);
            ProgressService.Record(5, Pass(11000), Mission);
            var r = ProgressService.Get(5);
            Assert.AreEqual(3, r.attempts);
            Assert.IsTrue(r.passed);
            Assert.AreEqual(11000, r.bestTimeMs);
            Assert.AreEqual(Mission.missionId, r.missionId);

            r.attempts = 99;
            Assert.AreEqual(3, ProgressService.Get(5).attempts, "Get returns a copy");
            Assert.AreEqual(0, ProgressService.Get(6).attempts, "never played → blank record");
        }

        [Test]
        public void AcademyScoreIsTheSumOfBestScores()
        {
            ProgressService.Record(0, Pass(), Mission);                  // 100
            ProgressService.Record(1, Pass(10000, "x_button"), Mission); // 92
            ProgressService.Record(1, Pass(10000, "x_button", "y_button"), Mission); // 84, not a best
            Assert.AreEqual(192, ProgressService.AcademyScore);
        }

        [Test]
        public void DemoUnlockAllOpensEveryLevelAndIsSaved()
        {
            ProgressService.DemoUnlockAll = true;
            Reload(NewKeyValueStore());
            Assert.IsTrue(ProgressService.DemoUnlockAll);
            for (int level = 0; level <= 7; level++)
                Assert.IsTrue(ProgressService.IsUnlocked(level), "L" + level);

            ProgressService.DemoUnlockAll = false;
            Assert.IsFalse(ProgressService.IsUnlocked(1));
        }

        [Test]
        public void ResetAllClearsProgressButKeepsTheCallsign()
        {
            ProgressService.Callsign = "Falcon";
            ProgressService.Record(0, Pass(), Mission);
            ProgressService.DemoUnlockAll = true;
            ProgressService.ResetAll();
            Reload(NewKeyValueStore());
            Assert.IsFalse(ProgressService.IsUnlocked(1));
            Assert.IsFalse(ProgressService.DemoUnlockAll);
            Assert.AreEqual(-1, ProgressService.BestScore(0));
            Assert.AreEqual("Falcon", ProgressService.Callsign);
        }

        [Test]
        public void CallsignIsTrimmedCappedAndSaved()
        {
            ProgressService.Callsign = "  Night Owl  ";
            Assert.AreEqual("Night Owl", ProgressService.Callsign);
            ProgressService.Callsign = new string('x', 40);
            Assert.AreEqual(24, ProgressService.Callsign.Length);
            ProgressService.Callsign = null;
            Reload(NewKeyValueStore());
            Assert.AreEqual("", ProgressService.Callsign);
        }

        [Test]
        public void MissingLevelsAreJumpedOver()
        {
            ProgressService.Initialize(NewKeyValueStore(), new[] { 0, 1, 2, 3, 7 });
            ProgressService.Record(3, Pass(), Mission);
            Assert.IsTrue(ProgressService.IsUnlocked(7));
            Assert.IsFalse(ProgressService.IsUnlocked(5));
        }

        [Test]
        public void ALevelAddedToTheBuildLaterStillUnlocksAfterThePreviousPass()
        {
            ProgressService.Initialize(NewKeyValueStore(), new[] { 0, 1, 2, 3, 5, 6, 7 });   // L4 not in the build yet
            ProgressService.Record(3, Pass(), Mission);
            Assert.IsTrue(ProgressService.IsUnlocked(5), "the missing level is jumped over");
            Reload(NewKeyValueStore());                                                    // next build has L4
            Assert.IsTrue(ProgressService.IsUnlocked(4), "passing L3 unlocks L4 once it exists");
            Assert.IsFalse(ProgressService.IsUnlocked(6));
        }

        [Test]
        public void RecordScoreGivesAPassAMedalEvenIfNoneWasSent()
        {
            ProgressService.RecordScore(1, 80, Rating.None, true);
            Assert.AreEqual(Rating.Silver, ProgressService.BestRating(1));
            ProgressService.RecordScore(2, 130, Rating.Gold, true);
            Assert.AreEqual(100, ProgressService.BestScore(2), "scores are clamped to 0..100");
        }

        [Test]
        public void RecordScoreUnlocksOnAPassAndIsIdempotent()
        {
            Assert.IsTrue(ProgressService.RecordScore(2, 88, Rating.Silver, true));
            Assert.IsTrue(ProgressService.IsUnlocked(3));
            Assert.IsTrue(ProgressService.IsPassed(2));
            Assert.IsFalse(ProgressService.RecordScore(2, 88, Rating.Silver, true), "same score again is not a new best");
            Assert.AreEqual(0, ProgressService.Get(2).attempts, "RecordScore does not count an attempt");
        }

        [Test]
        public void RecordScoreForAFailHasNoMedalAndUnlocksNothing()
        {
            ProgressService.RecordScore(2, 40, Rating.Gold, false);
            Assert.AreEqual(Rating.None, ProgressService.BestRating(2));
            Assert.IsFalse(ProgressService.IsUnlocked(3));
        }

        [Test]
        public void RecordNeverChangesTheResult()
        {
            var r = Fail();
            ProgressService.Record(0, r, Mission);
            Assert.IsFalse(r.passed);
            CollectionAssert.AreEqual(new[] { "s2" }, r.omittedCriticalSteps);
        }

        [Test]
        public void ChangedIsRaisedAfterARecord()
        {
            int calls = 0;
            Action onChanged = () => calls++;
            ProgressService.Changed += onChanged;
            try
            {
                ProgressService.Record(0, Pass(), Mission);
                Assert.AreEqual(1, calls);
            }
            finally
            {
                ProgressService.Changed -= onChanged;
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void NothingSavedLoadsFreshWithoutAWarning(string saved)
        {
            _saved = saved;
            var store = NewKeyValueStore();
            var state = store.Load();
            CollectionAssert.AreEqual(new[] { 0 }, state.unlockedLevels);
            Assert.IsNull(store.LastLoadWarning);
        }

        [TestCase("{not json")]
        [TestCase("null")]
        [TestCase("\u0000\u0001garbage")]
        public void CorruptSaveLoadsFreshNeverThrowsAndSaysWhy(string saved)
        {
            _saved = saved;
            var store = NewKeyValueStore();
            ProgressState state = null;
            Assert.DoesNotThrow(() => state = store.Load());
            CollectionAssert.AreEqual(new[] { 0 }, state.unlockedLevels);
            Assert.IsNotNull(store.LastLoadWarning);

            Assert.DoesNotThrow(() => ProgressService.Initialize(store));
            Assert.IsFalse(ProgressService.IsUnlocked(1));
        }

        [Test]
        public void AReadErrorLoadsFresh()
        {
            var store = new KeyValueProgressStore(() => throw new IOException("storage blocked"), _ => { },
                TestJson.ToJson, TestJson.FromJson<ProgressState>);
            Assert.DoesNotThrow(() => ProgressService.Initialize(store));
            Assert.IsTrue(ProgressService.IsUnlocked(0));
            StringAssert.Contains("storage blocked", store.LastLoadWarning);
        }

        [Test]
        public void AStoreThatThrowsOnLoadStillGivesAFreshState()
        {
            Assert.DoesNotThrow(() => ProgressService.Initialize(new ThrowingStore()));
            Assert.IsTrue(ProgressService.IsUnlocked(0));
            Assert.IsFalse(ProgressService.IsUnlocked(1));
        }

        [Test]
        public void AFailedSaveIsReportedAndProgressStaysInMemory()
        {
            ProgressService.Initialize(new ThrowingStore());
            Assert.DoesNotThrow(() => ProgressService.Record(0, Pass(), Mission));
            StringAssert.Contains("disk full", ProgressService.LastSaveError);
            Assert.IsTrue(ProgressService.IsUnlocked(1));
        }

        [Test]
        public void ASaveFromBeforeScoresExistedStillLoads()
        {
            _saved = "{\"version\":1,\"unlockedLevels\":[0,1,2],\"records\":[{\"level\":1,\"missionId\":\"l1-orientation-v1\",\"attempts\":2,\"passed\":true,\"bestTimeMs\":4200}]}";
            Reload(NewKeyValueStore());
            Assert.IsTrue(ProgressService.IsUnlocked(2));
            Assert.IsTrue(ProgressService.IsPassed(1));
            Assert.AreEqual(-1, ProgressService.BestScore(1));
            Assert.IsFalse(ProgressService.DemoUnlockAll);
            Assert.AreEqual("", ProgressService.Callsign);
        }

        [Test]
        public void ScoresAndMedalsRoundTripThroughJson()
        {
            ProgressService.Record(6, Pass(10000, "x_button", "y_button"), Mission);   // 84 Silver
            var json = _saved;
            StringAssert.Contains("bestScore", json);
            Reload(NewKeyValueStore());
            Assert.AreEqual(84, ProgressService.BestScore(6));
            Assert.AreEqual(Rating.Silver, ProgressService.BestRating(6));
        }

        private sealed class ThrowingStore : IProgressStore
        {
            public ProgressState Load() => throw new InvalidOperationException("broken store");
            public void Save(ProgressState state) => throw new IOException("disk full");
        }
    }
}

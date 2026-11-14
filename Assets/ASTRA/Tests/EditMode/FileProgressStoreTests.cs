using System;
using System.IO;
using Astra.Core.Assessment;
using Astra.Core.Progress;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>
    /// FileProgressStore: save/load round trip, corrupt files, and every crash point of a save (CONTRACT.md §6, AT-06) [F7].
    /// A "crash" is simulated by leaving exactly the files a crash at that point would leave behind.
    /// </summary>
    public class FileProgressStoreTests
    {
        private string _dir;

        private string MainFile => Path.Combine(_dir, "progress.json");
        private string TmpFile => MainFile + ".tmp";
        private string BakFile => MainFile + ".bak";

        [SetUp]
        public void CreateTempDirectory()
        {
            _dir = Path.Combine(Path.GetTempPath(), "astra-progress-tests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void DeleteTempDirectory()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }

        private FileProgressStore NewStore() =>
            new FileProgressStore(_dir, TestJson.ToJson, TestJson.FromJson<ProgressState>);

        private static ProgressState StateWithLevels(params int[] unlocked)
        {
            var state = new ProgressState();
            state.unlockedLevels.Clear();
            state.unlockedLevels.AddRange(unlocked);
            state.records.Add(new LevelRecord { level = 0, missionId = "l0-safety-v1", attempts = 2, passed = true, bestTimeMs = 4200 });
            return state;
        }

        private void WriteFile(string path, string text)
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(path, text);
        }

        private static void AssertFresh(ProgressState state)
        {
            CollectionAssert.AreEqual(new[] { 0 }, state.unlockedLevels);
            Assert.IsEmpty(state.records);
        }

        private static void AssertSameProgress(ProgressState expected, ProgressState actual)
        {
            Assert.AreEqual(expected.version, actual.version, "version");
            CollectionAssert.AreEqual(expected.unlockedLevels, actual.unlockedLevels, "unlockedLevels");
            Assert.AreEqual(expected.records.Count, actual.records.Count, "records");
            for (var i = 0; i < expected.records.Count; i++)
            {
                Assert.AreEqual(expected.records[i].level, actual.records[i].level, "level");
                Assert.AreEqual(expected.records[i].missionId, actual.records[i].missionId, "missionId");
                Assert.AreEqual(expected.records[i].attempts, actual.records[i].attempts, "attempts");
                Assert.AreEqual(expected.records[i].passed, actual.records[i].passed, "passed");
                Assert.AreEqual(expected.records[i].bestTimeMs, actual.records[i].bestTimeMs, "bestTimeMs");
            }
        }

        // --- Normal use ---

        [Test]
        public void NothingSavedYetGivesAFreshState()
        {
            var store = NewStore();
            AssertFresh(store.Load());
            Assert.IsNull(store.LastLoadWarning);
        }

        [Test]
        public void SavedProgressLoadsBackIdentically()
        {
            var saved = StateWithLevels(0, 1, 3);
            NewStore().Save(saved);

            var store = NewStore();
            AssertSameProgress(saved, store.Load());
            Assert.IsNull(store.LastLoadWarning);
        }

        [Test]
        public void SaveCreatesTheDirectoryAndLeavesNoTmpFile()
        {
            NewStore().Save(new ProgressState());
            Assert.IsTrue(File.Exists(MainFile));
            Assert.IsFalse(File.Exists(TmpFile));
        }

        [Test]
        public void SaveReplacesThePreviousSave()
        {
            var store = NewStore();
            store.Save(StateWithLevels(0, 1));
            store.Save(StateWithLevels(0, 1, 3));
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, store.Load().unlockedLevels);
        }

        [Test]
        public void ProgressSurvivesARecordedAttemptAndARestart()
        {
            var store = NewStore();
            var state = store.Load();
            ProgressRules.RecordAttempt(state, 0, "l0-safety-v1", new SessionResult { passed = true, completionTimeMs = 5000 }, new[] { 0, 1 });
            store.Save(state);

            var afterRestart = NewStore().Load();
            CollectionAssert.AreEqual(new[] { 0, 1 }, afterRestart.unlockedLevels);
            Assert.AreEqual(5000, afterRestart.records[0].bestTimeMs);
        }

        // --- Corrupt files ---

        [Test]
        public void CorruptFileIsBackedUpAndProgressStartsFresh()
        {
            WriteFile(MainFile, "{\"version\":1,\"unlockedLevels\":[0,1");
            var store = NewStore();

            AssertFresh(store.Load());
            Assert.IsFalse(File.Exists(MainFile), "corrupt file moved away");
            Assert.AreEqual("{\"version\":1,\"unlockedLevels\":[0,1", File.ReadAllText(BakFile), "kept as .bak");
            Assert.IsNotNull(store.LastLoadWarning);
        }

        [Test]
        public void EmptyFileCountsAsCorrupt()
        {
            WriteFile(MainFile, "");
            AssertFresh(NewStore().Load());
            Assert.IsTrue(File.Exists(BakFile));
        }

        [Test]
        public void AnOlderBackupIsReplaced()
        {
            WriteFile(BakFile, "old backup");
            WriteFile(MainFile, "not json");
            AssertFresh(NewStore().Load());
            Assert.AreEqual("not json", File.ReadAllText(BakFile));
        }

        [Test]
        public void ParserReturningNullCountsAsCorrupt()
        {
            WriteFile(MainFile, "{}");
            var store = new FileProgressStore(_dir, TestJson.ToJson, _ => null);
            AssertFresh(store.Load());
            Assert.IsTrue(File.Exists(BakFile));
        }

        [Test]
        public void SavingAfterACorruptLoadWorks()
        {
            WriteFile(MainFile, "garbage");
            var store = NewStore();
            store.Load();
            store.Save(StateWithLevels(0, 1));
            CollectionAssert.AreEqual(new[] { 0, 1 }, NewStore().Load().unlockedLevels);
        }

        [Test]
        public void LevelZeroIsAlwaysUnlockedAfterLoading()
        {
            WriteFile(MainFile, "{\"version\":1,\"unlockedLevels\":[3],\"records\":[]}");
            CollectionAssert.AreEqual(new[] { 0, 3 }, NewStore().Load().unlockedLevels);
        }

        // --- Crash points during Save: write tmp -> delete main -> move tmp to main ---

        [Test]
        public void CrashWhileWritingTmpKeepsTheOldSave()
        {
            var old = StateWithLevels(0, 1);
            NewStore().Save(old);
            WriteFile(TmpFile, "{\"version\":1,\"unlocked"); // half-written new save

            AssertSameProgress(old, NewStore().Load());
        }

        [Test]
        public void CrashBeforeDeletingTheOldFileKeepsTheOldSave()
        {
            var old = StateWithLevels(0, 1);
            NewStore().Save(old);
            WriteFile(TmpFile, TestJson.ToJson(StateWithLevels(0, 1, 3))); // complete new save, old not yet deleted

            AssertSameProgress(old, NewStore().Load());
        }

        [Test]
        public void CrashBetweenDeleteAndMoveRecoversTheNewSave()
        {
            var newer = StateWithLevels(0, 1, 3);
            WriteFile(TmpFile, TestJson.ToJson(newer)); // progress.json already deleted, tmp not yet moved
            var store = NewStore();

            AssertSameProgress(newer, store.Load());
            Assert.IsNotNull(store.LastLoadWarning, "recovery is reported");
        }

        [Test]
        public void CrashDuringTheVeryFirstSaveGivesAFreshState()
        {
            WriteFile(TmpFile, "{\"version\":1,\"unl"); // no progress.json ever existed
            AssertFresh(NewStore().Load());
        }

        [Test]
        public void SaveAfterARecoveredCrashCleansUp()
        {
            WriteFile(TmpFile, TestJson.ToJson(StateWithLevels(0, 1, 3)));
            var store = NewStore();
            var state = store.Load();
            store.Save(state);

            Assert.IsTrue(File.Exists(MainFile));
            Assert.IsFalse(File.Exists(TmpFile));
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, NewStore().Load().unlockedLevels);
        }

        // --- Arguments ---

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.Catch<ArgumentException>(() => new FileProgressStore("", TestJson.ToJson, TestJson.FromJson<ProgressState>));
            Assert.Catch<ArgumentException>(() => new FileProgressStore(_dir, null, TestJson.FromJson<ProgressState>));
            Assert.Catch<ArgumentException>(() => new FileProgressStore(_dir, TestJson.ToJson, null));
            Assert.Catch<ArgumentException>(() => NewStore().Save(null));
        }
    }
}

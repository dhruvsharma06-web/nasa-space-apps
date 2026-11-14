using System;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;

namespace Astra.Core.Progress
{
    /// <summary>What <see cref="ProgressService.Record"/> did with one scored attempt.</summary>
    public readonly struct RecordOutcome
    {
        public RecordOutcome(ScoreBreakdown breakdown, bool isNewBest, int previousBest)
        {
            Breakdown = breakdown;
            IsNewBest = isNewBest;
            PreviousBest = previousBest;
        }

        /// <summary>The 0–100 score, medal and lines of this attempt.</summary>
        public ScoreBreakdown Breakdown { get; }

        /// <summary>True when this attempt beat the stored best score (or was the first scored attempt).</summary>
        public bool IsNewBest { get; }

        /// <summary>The best score before this attempt; −1 if the level was never scored.</summary>
        public int PreviousBest { get; }
    }

    /// <summary>
    /// The one place the Hub, the debrief and the LevelController read and write the cadet's progress (TASKS S6).
    /// A thin facade over <see cref="ProgressRules"/>, <see cref="ResultRating"/> and one <see cref="IProgressStore"/>;
    /// the App picks the store per platform (files on Android, <see cref="KeyValueProgressStore"/> on WebGL).
    /// <para>Rules: level 0 is always unlocked; passing level N unlocks the next available level; "Demo: unlock all"
    /// opens every level and is saved. Each level keeps attempts, passed, best time, best score and its medal.
    /// The Academy score is the sum of the best scores. Nothing here ever changes a <see cref="SessionResult"/>.</para>
    /// <para>Never throws for a broken or missing save: it loads a fresh state. A failed save is reported in
    /// <see cref="LastSaveError"/> and the game carries on with the progress in memory.</para>
    /// Main thread only.
    /// </summary>
    public static class ProgressService
    {
        /// <summary>Levels 0..7 (CONTRACT §2).</summary>
        public static readonly IReadOnlyList<int> AllLevels = new[] { 0, 1, 2, 3, 4, 5, 6, 7 };

        private static IProgressStore _store;
        private static ProgressState _state;
        private static int[] _available = { 0, 1, 2, 3, 4, 5, 6, 7 };

        /// <summary>True after <see cref="Initialize(IProgressStore)"/>. Before that, progress lives in memory only.</summary>
        public static bool IsInitialized => _store != null;

        /// <summary>The reason the last save failed, or null if it worked.</summary>
        public static string LastSaveError { get; private set; }

        /// <summary>Raised after any change (record, unlock-all, callsign, reset), for screens that show progress.</summary>
        public static event Action Changed;

        /// <summary>Loads progress from <paramref name="store"/>; every level 0..7 is available.</summary>
        public static void Initialize(IProgressStore store) => Initialize(store, null);

        /// <summary>
        /// Loads progress from <paramref name="store"/>. <paramref name="availableLevels"/> = levels whose scene is in
        /// the build (null = 0..7); passing a level unlocks the next one in this list, so a missing level is jumped over.
        /// </summary>
        public static void Initialize(IProgressStore store, IEnumerable<int> availableLevels)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _available = availableLevels != null ? new List<int>(availableLevels).ToArray() : new[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            LastSaveError = null;
            _state = SafeLoad(store);
            Changed?.Invoke();
        }

        /// <summary>
        /// Level 0 always; level N once level N−1 is passed; any level unlocked earlier (passing jumps over a level that
        /// was missing from the build); every level while <see cref="DemoUnlockAll"/> is on.
        /// </summary>
        public static bool IsUnlocked(int level)
        {
            if (level == 0) return true;
            var state = State;
            return state.demoUnlockAll || state.unlockedLevels.Contains(level) || (level > 0 && IsPassed(level - 1));
        }

        public static bool IsPassed(int level) => Find(level)?.passed ?? false;

        /// <summary>A copy of the level's record (attempts, passed, best time, best score, medal); a blank one if never played.</summary>
        public static LevelRecord Get(int level)
        {
            var r = Find(level);
            if (r == null) return new LevelRecord { level = level };
            return new LevelRecord
            {
                level = r.level, missionId = r.missionId, attempts = r.attempts, passed = r.passed,
                bestTimeMs = r.bestTimeMs, scored = r.scored, bestScore = r.bestScore, bestRating = r.bestRating,
            };
        }

        /// <summary>The best score for a level, or −1 if it was never scored.</summary>
        public static int BestScore(int level)
        {
            var r = Find(level);
            return r != null && r.scored ? r.bestScore : -1;
        }

        /// <summary>The medal earned with the best score; <see cref="Rating.None"/> if none.</summary>
        public static Rating BestRating(int level)
        {
            var r = Find(level);
            return r != null && r.scored ? r.bestRating : Rating.None;
        }

        /// <summary>Sum of the best scores of all levels (shown in the Hub and on the certificate).</summary>
        public static int AcademyScore
        {
            get
            {
                int total = 0;
                foreach (var r in State.records)
                    if (r.scored) total += Math.Max(0, r.bestScore);
                return total;
            }
        }

        /// <summary>
        /// Records one scored Qualification attempt and saves: attempts + 1; on a pass, marks the level passed, keeps
        /// the best time and unlocks the next level; keeps the best score and its medal. Returns this attempt's
        /// breakdown and whether it is a new best. <paramref name="result"/> is not changed.
        /// </summary>
        public static RecordOutcome Record(int level, SessionResult result, MissionDefinition mission)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (mission == null) throw new ArgumentNullException(nameof(mission));

            var state = State;
            var breakdown = ResultRating.From(result, mission);
            ProgressRules.RecordAttempt(state, level, mission.missionId, result, _available);
            var record = Find(level);
            int previous = record.scored ? record.bestScore : -1;
            bool isNewBest = KeepBest(record, breakdown.Score, breakdown.Rating);
            SaveAndNotify();
            return new RecordOutcome(breakdown, isNewBest, previous);
        }

        /// <summary>
        /// Records an already-computed score without an attempt (for UI adapters that only have the score):
        /// keeps the best score/medal and, on a pass, marks the level passed and unlocks the next level.
        /// Recording the same score again changes nothing. Returns true when it beats the stored best.
        /// </summary>
        public static bool RecordScore(int level, int score, Rating rating, bool passed)
        {
            var state = State;
            var record = FindOrAdd(state, level);
            if (passed)
            {
                record.passed = true;
                var next = ProgressRules.NextAvailableLevel(level, _available);
                if (next.HasValue) ProgressRules.Unlock(state, next.Value);
            }
            score = Math.Max(0, Math.Min(100, score));
            if (!passed) rating = Rating.None;
            else if (rating == Rating.None) rating = ResultRating.RatingFor(score, true);   // a pass always has a medal
            bool isNewBest = KeepBest(record, score, rating);
            SaveAndNotify();
            return isNewBest;
        }

        /// <summary>Judges' "Demo: unlock all" (DECISIONS D11). Saved.</summary>
        public static bool DemoUnlockAll
        {
            get => State.demoUnlockAll;
            set
            {
                if (State.demoUnlockAll == value) return;
                State.demoUnlockAll = value;
                SaveAndNotify();
            }
        }

        /// <summary>The cadet's callsign ("" if not set). Free text, trimmed to 24 characters. Saved.</summary>
        public static string Callsign
        {
            get => State.callsign ?? "";
            set
            {
                var clean = (value ?? "").Trim();
                if (clean.Length > 24) clean = clean.Substring(0, 24);
                if (clean == State.callsign) return;
                State.callsign = clean;
                SaveAndNotify();
            }
        }

        /// <summary>Settings → reset progress: back to a fresh state (only level 0, no records, demo off). Keeps the callsign.</summary>
        public static void ResetAll()
        {
            var callsign = Callsign;
            _state = new ProgressState { callsign = callsign };
            SaveAndNotify();
        }

        /// <summary>A copy of the whole saved state, e.g. for a debug screen. Changing it has no effect.</summary>
        public static ProgressState Snapshot()
        {
            var s = State;
            var copy = new ProgressState
            {
                version = s.version, demoUnlockAll = s.demoUnlockAll, callsign = s.callsign,
                unlockedLevels = new List<int>(s.unlockedLevels), records = new List<LevelRecord>(),
            };
            foreach (var r in s.records) copy.records.Add(Get(r.level));
            return copy;
        }

        private static ProgressState State => _state ?? (_state = new ProgressState());

        private static LevelRecord Find(int level)
        {
            foreach (var r in State.records)
                if (r.level == level) return r;
            return null;
        }

        private static LevelRecord FindOrAdd(ProgressState state, int level)
        {
            var found = Find(level);
            if (found != null) return found;
            var added = new LevelRecord { level = level };
            state.records.Add(added);
            return added;
        }

        /// <summary>A passed attempt beats any attempt that did not pass; otherwise the higher score wins.</summary>
        private static bool KeepBest(LevelRecord record, int score, Rating rating)
        {
            bool newPassed = rating != Rating.None;
            bool oldPassed = record.bestRating != Rating.None;
            bool better = !record.scored
                          || (newPassed && !oldPassed)
                          || (newPassed == oldPassed && score > record.bestScore);
            if (!better) return false;
            record.scored = true;
            record.bestScore = score;
            record.bestRating = rating;
            return true;
        }

        private static ProgressState SafeLoad(IProgressStore store)
        {
            try
            {
                return ProgressRules.Sanitize(store.Load() ?? new ProgressState());
            }
            catch (Exception)
            {
                return new ProgressState();   // a store must not throw on load (CONTRACT §6); never block the cadet
            }
        }

        private static void SaveAndNotify()
        {
            if (_store != null)
            {
                try
                {
                    _store.Save(State);
                    LastSaveError = null;
                }
                catch (Exception ex)
                {
                    LastSaveError = $"{ex.GetType().Name}: {ex.Message}";
                }
            }
            Changed?.Invoke();
        }
    }
}

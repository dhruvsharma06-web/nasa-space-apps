using System;
using System.Collections.Generic;
using Astra.Core.Progress;
using UnityEngine;

namespace Astra.UI.Progress
{
    /// <summary>
    /// What the Hub and debrief read about the cadet: unlocks, passes, best score/medal, callsign.
    /// Adapter for P2's <c>ProgressService</c> (TASKS S6); P2 replaces <see cref="AcademyProgress.Current"/>.
    /// UI only reads it, except the judges' "Demo: unlock all" and the callsign. Recording a score is done by the
    /// App/P2 side after scoring (<see cref="RecordScore"/>), never by the UI.
    /// </summary>
    public interface IAcademyProgress
    {
        bool IsUnlocked(int level);
        bool IsPassed(int level);
        int BestScore(int level);       // -1 = never scored
        Medal BestMedal(int level);
        int TotalScore { get; }
        string Callsign { get; }
        bool AllUnlocked { get; }
        void UnlockAll();
        /// <summary>Stores a scored attempt; returns true when it beats the stored best.</summary>
        bool RecordScore(int level, int score, Medal medal, bool passed);
    }

    public static class AcademyProgress
    {
        private static IAcademyProgress current;

        /// <summary>The active provider. Defaults to the PlayerPrefs placeholder (works on Android and WebGL).</summary>
        public static IAcademyProgress Current
        {
            get => current ?? (current = new PlayerPrefsAcademyProgress());
            set
            {
                current = value;
                IsSupplied = value != null;
            }
        }

        /// <summary>True once P2 (or a test) supplied a provider; the Hub then never replaces it.</summary>
        public static bool IsSupplied { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
            IsSupplied = false;
        }
    }

    /// <summary>
    /// PLACEHOLDER until P2's ProgressService: L0 always unlocked, passing N unlocks N+1, best score/medal per level,
    /// demo unlock-all flag and callsign, all in PlayerPrefs. Unlocks recorded by Core's <see cref="IProgressStore"/>
    /// (if supplied) are honoured too.
    /// </summary>
    public sealed class PlayerPrefsAcademyProgress : IAcademyProgress
    {
        private const string Prefix = "astra.academy.";
        private readonly IProgressStore store;
        private ProgressState snapshot;

        public PlayerPrefsAcademyProgress(IProgressStore progressStore = null)
        {
            store = progressStore;
        }

        public bool IsUnlocked(int level)
        {
            if (level == 0 || AllUnlocked) return true;
            if (level < 0 || level > 7) return false;
            if (IsPassed(level - 1)) return true;
            var state = Snapshot();
            return state?.unlockedLevels != null && state.unlockedLevels.Contains(level);
        }

        public bool IsPassed(int level)
        {
            if (PlayerPrefs.GetInt(Prefix + "passed." + level, 0) == 1) return true;
            var state = Snapshot();
            if (state?.records == null) return false;
            foreach (var record in state.records)
                if (record != null && record.level == level && record.passed) return true;
            return false;
        }

        public int BestScore(int level) => PlayerPrefs.GetInt(Prefix + "best." + level, -1);
        public Medal BestMedal(int level) => (Medal)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "medal." + level, 0), 0, 3);

        public int TotalScore
        {
            get
            {
                var total = 0;
                for (var i = 0; i <= 7; i++) total += Mathf.Max(0, BestScore(i));
                return total;
            }
        }

        public string Callsign
        {
            get
            {
                var value = PlayerPrefs.GetString(Prefix + "callsign", string.Empty);
                return string.IsNullOrWhiteSpace(value) ? "CADET" : value.Trim().ToUpperInvariant();
            }
        }

        public void SetCallsign(string value)
        {
            PlayerPrefs.SetString(Prefix + "callsign", value ?? string.Empty);
            PlayerPrefs.Save();
        }

        public bool AllUnlocked => PlayerPrefs.GetInt(Prefix + "unlockAll", 0) == 1;

        public void UnlockAll()
        {
            PlayerPrefs.SetInt(Prefix + "unlockAll", 1);
            PlayerPrefs.Save();
        }

        public bool RecordScore(int level, int score, Medal medal, bool passed)
        {
            if (level < 0 || level > 7) return false;
            if (passed) PlayerPrefs.SetInt(Prefix + "passed." + level, 1);
            var isBest = score > BestScore(level);
            if (isBest)
            {
                PlayerPrefs.SetInt(Prefix + "best." + level, score);
                PlayerPrefs.SetInt(Prefix + "medal." + level, (int)medal);
            }
            PlayerPrefs.Save();
            return isBest;
        }

        private ProgressState Snapshot()
        {
            if (store == null) return null;
            if (snapshot != null) return snapshot;
            try { snapshot = store.Load(); }
            catch (Exception exception) { Debug.LogWarning("[AcademyProgress] Progress store unavailable: " + exception.Message); }
            return snapshot;
        }
    }

    /// <summary>In-memory provider for tests and previews.</summary>
    public sealed class InMemoryAcademyProgress : IAcademyProgress
    {
        private readonly Dictionary<int, (int score, Medal medal)> best = new Dictionary<int, (int, Medal)>();
        private readonly HashSet<int> passed = new HashSet<int>();

        public string Callsign { get; set; } = "CADET";
        public bool AllUnlocked { get; private set; }
        public bool IsUnlocked(int level) => level == 0 || AllUnlocked || (level > 0 && level <= 7 && passed.Contains(level - 1));
        public bool IsPassed(int level) => passed.Contains(level);
        public int BestScore(int level) => best.TryGetValue(level, out var b) ? b.score : -1;
        public Medal BestMedal(int level) => best.TryGetValue(level, out var b) ? b.medal : Medal.None;

        public int TotalScore
        {
            get
            {
                var total = 0;
                foreach (var b in best.Values) total += b.score;
                return total;
            }
        }

        public void UnlockAll() => AllUnlocked = true;

        public bool RecordScore(int level, int score, Medal medal, bool didPass)
        {
            if (didPass) passed.Add(level);
            if (score <= BestScore(level)) return false;
            best[level] = (score, medal);
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using Astra.Core.Assessment;

namespace Astra.Core.Progress
{
    /// <summary>
    /// How a scored Qualification attempt changes saved progress (CONTRACT.md §6, FR-APP-05).
    /// Pure logic: no files, no clock. FileProgressStore does the saving.
    /// </summary>
    public static class ProgressRules
    {
        /// <summary>
        /// Records one attempt in <paramref name="state"/> (modified in place):
        /// attempts + 1; on a pass, marks the level passed, keeps the lowest passing time in bestTimeMs,
        /// and unlocks the next level number found in <paramref name="availableLevels"/>
        /// (levels whose scene is in Build Settings), so a missing level is jumped over.
        /// A failed attempt never removes a pass or a best time that was already earned.
        /// </summary>
        public static void RecordAttempt(
            ProgressState state, int level, string missionId, SessionResult result, IEnumerable<int> availableLevels)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (availableLevels == null)
                throw new ArgumentNullException(nameof(availableLevels));
            Sanitize(state);

            var record = FindOrAddRecord(state, level);
            record.missionId = missionId;
            record.attempts++;

            if (!result.passed)
                return;

            if (!record.passed || result.completionTimeMs < record.bestTimeMs)
                record.bestTimeMs = result.completionTimeMs;
            record.passed = true;

            var next = NextAvailableLevel(level, availableLevels);
            if (next.HasValue)
                Unlock(state, next.Value);
        }

        /// <summary>The smallest available level number above <paramref name="level"/>, or null if none.</summary>
        internal static int? NextAvailableLevel(int level, IEnumerable<int> availableLevels)
        {
            int? next = null;
            foreach (var candidate in availableLevels)
            {
                if (candidate > level && (next == null || candidate < next.Value))
                    next = candidate;
            }
            return next;
        }

        /// <summary>Adds a level to unlockedLevels, keeping the list sorted and free of duplicates.</summary>
        internal static void Unlock(ProgressState state, int level)
        {
            var levels = state.unlockedLevels;
            var at = 0;
            while (at < levels.Count && levels[at] < level)
                at++;
            if (at < levels.Count && levels[at] == level)
                return;
            levels.Insert(at, level);
        }

        /// <summary>
        /// Repairs a state read from disk so the rest of the code can trust it: missing lists become empty,
        /// null records are dropped, and level 0 is always unlocked (a cadet can never be locked out).
        /// </summary>
        internal static ProgressState Sanitize(ProgressState state)
        {
            if (state.unlockedLevels == null)
                state.unlockedLevels = new List<int>();
            if (state.records == null)
                state.records = new List<LevelRecord>();
            state.records.RemoveAll(r => r == null);
            Unlock(state, 0);
            return state;
        }

        private static LevelRecord FindOrAddRecord(ProgressState state, int level)
        {
            foreach (var record in state.records)
            {
                if (record.level == level)
                    return record;
            }
            var added = new LevelRecord { level = level };
            state.records.Add(added);
            return added;
        }
    }
}

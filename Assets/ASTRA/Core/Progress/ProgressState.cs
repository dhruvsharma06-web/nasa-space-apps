using System;
using System.Collections.Generic;

namespace Astra.Core.Progress
{
    /// <summary>Local saved progress (FR-APP-05, CONTRACT.md §6). No personal data.</summary>
    [Serializable]
    public sealed class ProgressState
    {
        public int version = 1;
        public List<int> unlockedLevels = new List<int> { 0 };
        public List<LevelRecord> records = new List<LevelRecord>();
    }

    [Serializable]
    public sealed class LevelRecord
    {
        public int level;
        public string missionId;
        public int attempts;
        public bool passed;
        /// <summary>Best completionTimeMs among passed attempts; 0 if never passed.</summary>
        public long bestTimeMs;
    }

    /// <summary>
    /// Load never throws: a missing file gives a fresh state (only level 0 unlocked);
    /// a corrupt file is renamed to *.bak and a fresh state is returned (AT-06).
    /// </summary>
    public interface IProgressStore
    {
        ProgressState Load();
        void Save(ProgressState state);
    }
}

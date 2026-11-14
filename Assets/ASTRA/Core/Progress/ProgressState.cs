using System;
using System.Collections.Generic;
using Astra.Core.Assessment;

namespace Astra.Core.Progress
{
    /// <summary>Local saved progress (FR-APP-05, CONTRACT.md §6). No personal data.</summary>
    [Serializable]
    public sealed class ProgressState
    {
        public int version = 1;
        public List<int> unlockedLevels = new List<int> { 0 };
        public List<LevelRecord> records = new List<LevelRecord>();
        /// <summary>Judges' "Demo: unlock all" (DECISIONS D11): every level opens. Additive to CONTRACT §6.</summary>
        public bool demoUnlockAll;
        /// <summary>Free-text callsign shown in the Hub and on the certificate; never a real name or email.</summary>
        public string callsign = "";
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
        /// <summary>True once an attempt was scored; bestScore and bestRating are only meaningful then.</summary>
        public bool scored;
        /// <summary>Best 0–100 score from ResultRating (docs/SCORING.md). Additive to CONTRACT §6.</summary>
        public int bestScore;
        /// <summary>The medal earned with bestScore.</summary>
        public Rating bestRating;
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

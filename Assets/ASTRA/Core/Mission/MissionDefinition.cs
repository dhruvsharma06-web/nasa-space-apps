using System;
using System.Collections.Generic;

namespace Astra.Core.Mission
{
    /// <summary>Versioned, data-defined mission (FR-SIM-02). Shape is fixed by CONTRACT.md §2.</summary>
    [Serializable]
    public sealed class MissionDefinition
    {
        public string missionId;
        public int level;
        public string version;
        public string title;
        public bool fictional = true;
        public List<MissionStep> steps = new List<MissionStep>();
    }

    [Serializable]
    public sealed class MissionStep
    {
        public string stepId;
        public int order;
        public string instruction;
        public string hint;
        public string expectedEvent;
        public string targetId;
        public bool critical;
    }
}

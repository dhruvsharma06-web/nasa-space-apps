using System.Collections.Generic;

namespace Astra.UI.Hub
{
    /// <summary>Presentation names, card details and scene routes from CONTRACT.md §2; no unlock or scoring rules.</summary>
    public static class LevelCatalog
    {
        public readonly struct Entry
        {
            public readonly int Level;
            public readonly string Title;
            public readonly string SceneName;
            /// <summary>One-line goal shown on the card.</summary>
            public readonly string Goal;
            /// <summary>The skill the level teaches.</summary>
            public readonly string Skill;
            /// <summary>1–3 dots.</summary>
            public readonly int Difficulty;
            /// <summary>Typical duration, e.g. "~2 min".</summary>
            public readonly string Duration;

            public Entry(int level, string title, string sceneName)
                : this(level, title, sceneName, string.Empty, string.Empty, 1, string.Empty) { }

            public Entry(int level, string title, string sceneName, string goal, string skill, int difficulty, string duration)
            {
                Level = level;
                Title = title;
                SceneName = sceneName;
                Goal = goal;
                Skill = skill;
                Difficulty = difficulty < 1 ? 1 : difficulty > 3 ? 3 : difficulty;
                Duration = duration;
            }

            /// <summary>"●●○" style dots; plain characters so the built-in font renders them.</summary>
            public string DifficultyDots => new string('*', Difficulty) + new string('-', 3 - Difficulty);
        }

        public static readonly IReadOnlyList<Entry> Levels = System.Array.AsReadOnly(new[]
        {
            new Entry(0, "Safety & Calibration", "L0_Safety", "Learn to look, tap, flip, turn and place.", "Gaze controls", 1, "~2 min"),
            new Entry(1, "Cadet Orientation", "L1_Orientation", "Find the four cabin panels around your seat.", "Cabin layout", 1, "~2 min"),
            new Entry(2, "Equipment Handling", "L2_Equipment", "Stow each tool in its matching bay.", "Tool handling", 2, "~3 min"),
            new Entry(3, "Cabin Familiarization", "L3_Cabin", "Set up cabin lights, air and comms.", "Cabin systems", 2, "~3 min"),
            new Entry(4, "Emergency Response", "L4_Emergency", "Clear a power-bus fault safely.", "Fault response", 3, "~3 min"),
            new Entry(5, "EVA & Tethering", "L5_EVA", "Prepare and tether for a spacewalk.", "EVA preparation", 2, "~3 min"),
            new Entry(6, "Robotics Arm", "L6_Robotics", "Operate the arm to capture a payload.", "Robotics", 3, "~4 min"),
            new Entry(7, "Integrated Mission", "L7_Finale", "Restore station power under pressure.", "Everything together", 3, "~4 min")
        });
    }
}

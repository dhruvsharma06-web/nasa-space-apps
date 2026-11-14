using System.Collections.Generic;

namespace Astra.UI.Hub
{
    /// <summary>Presentation names and scene routes from CONTRACT.md §2; no unlock or scoring rules.</summary>
    public static class LevelCatalog
    {
        public readonly struct Entry
        {
            public readonly int Level;
            public readonly string Title;
            public readonly string SceneName;

            public Entry(int level, string title, string sceneName)
            {
                Level = level;
                Title = title;
                SceneName = sceneName;
            }
        }

        public static readonly IReadOnlyList<Entry> Levels = System.Array.AsReadOnly(new[]
        {
            new Entry(0, "Safety & Calibration", "L0_Safety"),
            new Entry(1, "Cadet Orientation", "L1_Orientation"),
            new Entry(2, "Equipment Handling", "L2_Equipment"),
            new Entry(3, "Cabin Familiarization", "L3_Cabin"),
            new Entry(4, "Emergency Response", "L4_Emergency"),
            new Entry(5, "EVA & Tethering", "L5_EVA"),
            new Entry(6, "Robotics Arm", "L6_Robotics"),
            new Entry(7, "Integrated Mission", "L7_Finale")
        });
    }
}

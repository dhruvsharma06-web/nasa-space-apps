using Astra.UI.Progress;
using Astra.XR.Phone;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.UI.Hub
{
    /// <summary>
    /// Hub header and side cards: callsign, total academy score, medals earned, "Continue: L3 Cabin", the judges'
    /// "Demo: unlock all" button and the dwell-select setting. Reads <see cref="IAcademyProgress"/>; the only writes
    /// are the demo unlock flag and the player's dwell preference.
    /// </summary>
    public sealed class HubHeader : MonoBehaviour
    {
        [SerializeField] private HubController hub;
        [SerializeField] private Text callsignLabel;
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text medalsLabel;
        [SerializeField] private Text continueLabel;
        [SerializeField] private Text dwellLabel;
        [SerializeField] private Text unlockLabel;

        public void Configure(HubController controller, Text callsign, Text score, Text medals, Text continueText, Text dwell, Text unlock)
        {
            hub = controller;
            callsignLabel = callsign;
            scoreLabel = score;
            medalsLabel = medals;
            continueLabel = continueText;
            dwellLabel = dwell;
            unlockLabel = unlock;
        }

        private void OnEnable()
        {
            if (hub != null) hub.Refreshed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (hub != null) hub.Refreshed -= Refresh;
        }

        public void Refresh()
        {
            var progress = hub != null && hub.Academy != null ? hub.Academy : AcademyProgress.Current;
            if (callsignLabel != null) callsignLabel.text = progress.Callsign;
            if (scoreLabel != null) scoreLabel.text = progress.TotalScore.ToString();
            if (medalsLabel != null)
            {
                int gold = 0, silver = 0, bronze = 0;
                for (var i = 0; i < LevelCatalog.Levels.Count; i++)
                {
                    var medal = progress.BestMedal(i);
                    if (medal == Medal.Gold) gold++;
                    else if (medal == Medal.Silver) silver++;
                    else if (medal == Medal.Bronze) bronze++;
                }
                medalsLabel.text = $"{gold}G {silver}S {bronze}B";
            }
            if (continueLabel != null)
            {
                var next = hub != null ? hub.RecommendedLevel : -1;
                var allPassed = true;
                for (var i = 0; i < LevelCatalog.Levels.Count; i++) allPassed &= progress.IsPassed(i);
                continueLabel.text = next >= 0 ? $"Continue  \u00b7  L{next} {LevelCatalog.Levels[next].Title}"
                    : allPassed ? "All levels passed" : "Choose a level to begin";
            }
            if (dwellLabel != null) dwellLabel.text = GazeSettings.DwellSelect ? "On" : "Off";
            if (unlockLabel != null) unlockLabel.text = progress.AllUnlocked ? "All levels unlocked" : "Unlock all levels";
        }

        /// <summary>Settings card button.</summary>
        public void ToggleDwell()
        {
            GazeSettings.DwellSelect = !GazeSettings.DwellSelect;
            Refresh();
        }

        /// <summary>Judges' button: every level becomes selectable (when its scene is in the build).</summary>
        public void UnlockAll()
        {
            if (hub != null) hub.DemoUnlockAll();
            else AcademyProgress.Current.UnlockAll();
            Refresh();
        }

        private static string ShortTitle(string title)
        {
            var space = title.IndexOf(' ');
            return space > 0 ? title.Substring(0, space) : title;
        }
    }
}

using System;
using Astra.UI.Progress;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Astra.UI
{
    /// <summary>
    /// Graduation certificate: "ASTRA ACADEMY: CADET QUALIFIED", callsign, date, total academy score, the eight level
    /// medals, the fictional ASTRA STATION crest and the disclaimer line. Read-only presentation of progress.
    /// </summary>
    public sealed class CertificatePanel : MonoBehaviour
    {
        public const string Heading = "ASTRA ACADEMY: CADET QUALIFIED";
        public const string Disclaimer = "Fictional training, not an official certification";

        [SerializeField] private GameObject root;
        [SerializeField] private Text callsignLabel;
        [SerializeField] private Text dateLabel;
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text[] medalLabels = new Text[8];
        [SerializeField] private Graphic[] medalBadges = new Graphic[8];
        [SerializeField] private UnityEvent closed = new UnityEvent();

        public UnityEvent onClosed => closed;

        public void Configure(GameObject panelRoot, Text callsign, Text date, Text score, Text[] medals, Graphic[] badges)
        {
            root = panelRoot;
            callsignLabel = callsign;
            dateLabel = date;
            scoreLabel = score;
            medalLabels = medals ?? new Text[0];
            medalBadges = badges ?? new Graphic[0];
        }

        private void Awake()
        {
            if (root != null && root != gameObject) root.SetActive(false);
        }

        public void Show(IAcademyProgress progress)
        {
            progress = progress ?? AcademyProgress.Current;
            if (callsignLabel != null) callsignLabel.text = progress.Callsign;
            if (dateLabel != null) dateLabel.text = DateTime.Now.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
            if (scoreLabel != null) scoreLabel.text = "ACADEMY SCORE  " + progress.TotalScore;
            for (var level = 0; level < medalLabels.Length; level++)
            {
                var medal = progress.BestMedal(level);
                if (level < medalLabels.Length && medalLabels[level] != null)
                    medalLabels[level].text = "L" + level + "\n" + (medal == Medal.None ? "-" : medal.ToString().ToUpperInvariant());
                if (level < medalBadges.Length && medalBadges[level] != null)
                    medalBadges[level].color = medal == Medal.Gold ? Theme.Gold : medal == Medal.Silver ? Theme.Silver
                        : medal == Medal.Bronze ? Theme.Bronze : new Color(0.25f, 0.28f, 0.33f);
            }
            if (root != null) root.SetActive(true);
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
            closed.Invoke();
        }
    }
}

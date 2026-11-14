using System.Collections;
using System.Linq;
using Astra.UI.Gaze;
using Astra.UI.Hub;
using Astra.UI.Progress;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Astra.UI.Tests.PlayMode
{
    /// <summary>
    /// Loads the generated Hub scene: 8 cards exist, locked cards cannot be selected (by click or by gaze), and
    /// "Demo: unlock all" unlocks every level. Uses an in-memory progress provider, so real saves are untouched.
    /// </summary>
    public class HubSceneTests
    {
        private const string HubPath = "Assets/ASTRA/Scenes/Hub.unity";

        [UnityTest]
        public IEnumerator HubHasEightCardsLocksAreRespectedAndUnlockAllUnlocks()
        {
#if UNITY_EDITOR
            if (!System.IO.File.Exists(HubPath))
                Assert.Ignore("Hub.unity is not generated (ASTRA → P1 → Rebuild P1 assets and Hub scene).");
            var progress = new InMemoryAcademyProgress();
            AcademyProgress.Current = progress;
            var startScene = SceneManager.GetActiveScene().name;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(HubPath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;

            var cards = Object.FindObjectsByType<LevelCard>(FindObjectsSortMode.None).OrderBy(c => c.Level).ToArray();
            Assert.That(cards.Length, Is.EqualTo(8), "one card per level");
            Assert.That(cards.Select(c => c.Level), Is.EqualTo(Enumerable.Range(0, 8)));
            var hub = Object.FindAnyObjectByType<HubController>();
            Assert.That(hub, Is.Not.Null);
            Assert.That(hub.Academy, Is.SameAs(progress), "the Hub must read the supplied progress provider");

            foreach (var card in cards)
            {
                var status = card.GetComponentsInChildren<Text>(true).First(t => t.name == "Status").text;
                if (!status.StartsWith("Locked"))
                    continue;
                var button = card.GetComponent<Button>();
                Assert.That(button.interactable, Is.False, $"L{card.Level} is locked but interactable");
                var gaze = card.GetComponent<GazeButton>();
                Assert.That(gaze.IsGazeEnabled, Is.False, $"L{card.Level} is locked but gaze-selectable");
                button.onClick.Invoke();
                gaze.ActivateFromGaze();
                yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Hub"), $"selecting locked L{card.Level} loaded a scene");
            }

            var header = Object.FindAnyObjectByType<HubHeader>();
            Assert.That(header, Is.Not.Null, "Demo: unlock all lives on the Hub header");
            header.UnlockAll();
            yield return null;
            Assert.That(progress.AllUnlocked, Is.True);
            foreach (var card in cards)
            {
                var status = card.GetComponentsInChildren<Text>(true).First(t => t.name == "Status").text;
                Assert.That(status.StartsWith("Locked"), Is.False, $"L{card.Level} still locked after unlock all ({status})");
            }
            AcademyProgress.Current = null;
            Assert.That(startScene, Is.Not.Null);
#else
            Assert.Ignore("Editor-only scene test.");
            yield break;
#endif
        }
    }
}

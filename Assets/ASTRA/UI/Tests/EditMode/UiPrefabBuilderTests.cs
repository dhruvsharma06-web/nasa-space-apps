using System;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.UI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Astra.UI.Tests.EditMode
{
    public class UiPrefabBuilderTests
    {
        private string directory;

        // Build the prefabs once per fixture: creating and deleting them for every test makes Windows file
        // watchers lock the fresh .meta files ("Cannot open file ... for write").
        [OneTimeSetUp]
        public void SetUp()
        {
            directory = "Assets/ASTRA/UI/Tests/Generated_" + Guid.NewGuid().ToString("N");
            UiPrefabBuilder.BuildInDirectory(directory);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(directory)) AssetDatabase.DeleteAsset(directory);
        }

        [Test]
        public void BuildsFourWorldSpaceReadablePrefabs()
        {
            CheckPrefab<InstructionPanel>("InstructionPanel");
            CheckPrefab<AlarmIndicator>("AlarmIndicator");
            CheckPrefab<PhaseBanner>("PhaseBanner");
            CheckPrefab<DebriefPanel>("DebriefPanel");
        }

        [Test]
        public void InstantiatedPanelsDisplayLiteralContentAndButtonsInvokeOnce()
        {
            var instruction = Object.Instantiate(Load("InstructionPanel"));
            var debrief = Object.Instantiate(Load("DebriefPanel"));
            try
            {
                instruction.GetComponent<InstructionPanel>().ShowInstruction("<b>literal</b>");
                var instructionText = instruction.transform.Find("Instruction").GetComponent<Text>();
                Assert.That(instructionText.text, Is.EqualTo("<b>literal</b>"));
                Assert.That(instructionText.supportRichText, Is.False);
                var panel = debrief.GetComponent<DebriefPanel>();
                panel.Show(new SessionResult { missionId = "mission", passed = true }, new MissionDefinition { missionId = "mission" });
                panel.ShowCoachText("<b>advice</b>", true);
                var coach = debrief.transform.Find("CoachViewport/CoachContent").GetComponent<Text>();
                Assert.That(coach.text, Is.EqualTo("<b>advice</b>"));
                Assert.That(coach.supportRichText, Is.False);
                Assert.That(debrief.transform.Find("Outcome").GetComponent<Text>().text, Is.EqualTo("PASSED"));
                var retries = 0;
                var exits = 0;
                panel.onRetry.AddListener(() => retries++);
                panel.onBackToHub.AddListener(() => exits++);
                var retry = debrief.transform.Find("Retry").GetComponent<Button>();
                var hub = debrief.transform.Find("BackToHub").GetComponent<Button>();
                Assert.That(retry.onClick.GetPersistentEventCount(), Is.EqualTo(1));
                Assert.That(hub.onClick.GetPersistentEventCount(), Is.EqualTo(1));
                // Production button listeners are RuntimeOnly. Verify serialization here;
                // actual invocation is exercised by PrefabInteractionTests in PlayMode.
                Assert.That(retry.onClick.GetPersistentTarget(0), Is.SameAs(panel));
                Assert.That(hub.onClick.GetPersistentTarget(0), Is.SameAs(panel));
                Assert.That(retry.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(DebriefPanel.RequestRetry)));
                Assert.That(hub.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(DebriefPanel.RequestBackToHub)));
                var next = debrief.transform.Find("NextLevel").GetComponent<Button>();
                Assert.That(next.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(DebriefPanel.RequestNextLevel)));
                panel.RequestRetry();
                panel.RequestBackToHub();
                Assert.That(retries, Is.EqualTo(1));
                Assert.That(exits, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(instruction); Object.DestroyImmediate(debrief); }
        }

        [Test]
        public void DebriefHasClippedVerticalScrollAreasAndReachableButtons()
        {
            var prefab = Load("DebriefPanel");
            var scrolls = prefab.GetComponentsInChildren<ScrollRect>(true);
            Assert.That(scrolls.Length, Is.EqualTo(2));
            foreach (var scroll in scrolls)
            {
                Assert.That(scroll.viewport, Is.Not.Null);
                Assert.That(scroll.content, Is.Not.Null);
                Assert.That(scroll.content.IsChildOf(scroll.viewport), Is.True);
                Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(scroll.vertical, Is.True);
                Assert.That(scroll.horizontal, Is.False);
            }
            var buttons = prefab.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Length, Is.EqualTo(3), "RETRY, NEXT LEVEL and HUB");
            foreach (var button in buttons)
                Assert.That(button.GetComponent<Astra.UI.Gaze.GazeButton>(), Is.Not.Null, button.name + " must be gaze-selectable");
            foreach (var button in buttons)
            {
                var rect = (RectTransform)button.transform;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Assert.That(Vector3.Distance(corners[0], corners[3]), Is.GreaterThanOrEqualTo(0.06f));
                Assert.That(Vector3.Distance(corners[0], corners[1]), Is.GreaterThanOrEqualTo(0.06f));
            }
        }

        [TestCase("InstructionPanel", 0.42f, "Instruction", Theme.MinBodyMetres)]
        [TestCase("InstructionPanel", 0.42f, "Hint", Theme.MinBodyMetres)]
        [TestCase("PhaseBanner", 0.34f, "Phase", Theme.MinTitleMetres)]
        [TestCase("DebriefPanel", 0.46f, "Outcome", Theme.MinTitleMetres)]
        [TestCase("DebriefPanel", 0.46f, "DetailsViewport/DetailsContent", Theme.MinBodyMetres)]
        [TestCase("AlarmIndicator", 0.28f, "Caption", Theme.MinBodyMetres)]
        public void TextIsLegibleOnThePhoneAtTheFactoryWidth(string prefabName, float factoryWidth, string path, float minimumMetres)
        {
            var prefab = Load(prefabName);
            var rect = (RectTransform)prefab.transform;
            var metresPerUnit = factoryWidth / rect.sizeDelta.x;
            var text = prefab.transform.Find(path).GetComponent<Text>();
            // Cap height of the built-in sans font is about 0.7 em.
            Assert.That(text.fontSize * 0.7f * metresPerUnit, Is.GreaterThanOrEqualTo(minimumMetres), $"{prefabName}/{path}");
        }

        [Test]
        public void PanelsAreTransparentBackedForTheFactoryBackplate()
        {
            foreach (var name in new[] { "InstructionPanel", "PhaseBanner", "DebriefPanel", "AlarmIndicator" })
                Assert.That(Load(name).GetComponent<Image>().color.a, Is.LessThan(0.05f), name);
        }

        [Test]
        public void RegenerationPreservesAuthoredPrefabAndGuid()
        {
            var path = directory + "/InstructionPanel.prefab";
            var guid = AssetDatabase.AssetPathToGUID(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                new GameObject("AuthoredSentinel").transform.SetParent(root.transform, false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            UiPrefabBuilder.BuildInDirectory(directory);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(Load("InstructionPanel").transform.Find("AuthoredSentinel"), Is.Not.Null);
        }

        [TestCase("Assets/ASTRA/UI/../../Core")]
        [TestCase("Assets/ASTRA/Core/Generated")]
        [TestCase("Assets/ASTRA/UI/Tests/../Generated")]
        public void RejectsPathsOutsideOwnedDirectory(string path)
        {
            Assert.Throws<ArgumentException>(() => UiPrefabBuilder.BuildInDirectory(path));
        }

        private GameObject Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(directory + "/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, name);
            return prefab;
        }

        private void CheckPrefab<T>(string name) where T : Component
        {
            var prefab = Load(name);
            Assert.That(prefab.GetComponent<T>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            var background = prefab.GetComponent<Image>().color;
            var texts = prefab.GetComponentsInChildren<Text>(true);
            Assert.That(texts.Length, Is.GreaterThan(0));
            foreach (var text in texts)
            {
                Assert.That(text.font, Is.Not.Null);
                Assert.That(text.supportRichText, Is.False);
                Assert.That(text.color.grayscale - background.grayscale, Is.GreaterThan(0.7f));
            }
        }
    }
}

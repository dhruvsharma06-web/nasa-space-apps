using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Astra.UI.Tests.PlayMode
{
    public sealed class PanelPresentationTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            foreach (var instance in objects)
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            objects.Clear();
        }

        [Test]
        public void InstructionPanelShowsInstructionAndClearsOnlyHint()
        {
            var instruction = NewText("Instruction");
            var hint = NewText("Hint");
            var panelObject = NewObject("InstructionPanel");
            var panel = panelObject.AddComponent<InstructionPanel>();
            panel.Configure(instruction, hint);
            panelObject.SetActive(true);

            panel.ShowInstruction("Read the console.");
            panel.ShowHint("Try the marked control.");
            Assert.AreEqual("Read the console.", instruction.text);
            Assert.AreEqual("Try the marked control.", hint.text);
            Assert.IsTrue(hint.enabled);
            Assert.IsFalse(instruction.supportRichText);

            panel.ClearHint();
            Assert.IsTrue(panel.enabled);
            Assert.IsTrue(instruction.enabled);
            Assert.AreEqual("Read the console.", instruction.text);
            Assert.AreEqual(string.Empty, hint.text);
            Assert.IsFalse(hint.enabled);
        }

        [Test]
        public void PhaseBannerShowsEveryPhase_AndLogsInvalidValue()
        {
            var label = NewText("PhaseLabel");
            var bannerObject = NewObject("PhaseBanner");
            var banner = bannerObject.AddComponent<PhaseBanner>();
            banner.Configure(label);
            bannerObject.SetActive(true);

            var expected = new[] { "Briefing", "Demonstration", "Practice", "Qualification", "Debrief" };
            var phases = new[] { ModulePhase.Briefing, ModulePhase.Demonstration, ModulePhase.Practice,
                ModulePhase.Qualification, ModulePhase.Debrief };
            for (var i = 0; i < phases.Length; i++)
            {
                banner.Show(phases[i]);
                Assert.AreEqual(expected[i], label.text);
            }

            LogAssert.Expect(LogType.Error, $"[PhaseBanner] Invalid module phase: {(ModulePhase)999}.");
            banner.Show((ModulePhase)999);
            Assert.AreEqual(string.Empty, label.text);
        }

        [UnityTest]
        public IEnumerator AlarmKeepsCaptionVisible_AndFlashesAtBoundedRateOnUnscaledTime()
        {
            var caption = NewText("AlarmCaption");
            var light = NewGraphic("AlarmLight");
            var alarmObject = NewObject("AlarmIndicator");
            var alarm = alarmObject.AddComponent<AlarmIndicator>();
            alarm.Configure(caption, light, 3f);
            alarmObject.SetActive(true);
            Time.timeScale = 0f;

            alarm.SetAlarm(true, "Pressure warning");
            Assert.AreEqual("Pressure warning", caption.text);
            Assert.IsTrue(caption.enabled);
            var lastLight = light.enabled;
            var transitions = 0;
            var observationStarted = Time.realtimeSinceStartupAsDouble;
            for (var i = 0; i < 40; i++)
            {
                yield return new WaitForSecondsRealtime(0.025f);
                Assert.AreEqual("Pressure warning", caption.text);
                Assert.IsTrue(caption.enabled);
                if (light.enabled != lastLight)
                {
                    transitions++;
                    lastLight = light.enabled;
                }
            }
            var elapsed = Time.realtimeSinceStartupAsDouble - observationStarted;
            Assert.Greater(transitions, 0, "The alarm should visibly flash while timeScale is zero.");
            Assert.LessOrEqual(transitions, Math.Ceiling(elapsed * 6.0) + 1.0,
                "The flashing rate must not exceed three complete flashes per real-time second.");

            alarm.SetAlarm(false, "ignored while off");
            Assert.IsFalse(alarm.IsAlarmActive);
            Assert.IsFalse(caption.enabled);
            Assert.AreEqual(string.Empty, caption.text);
            Assert.IsFalse(light.enabled);
        }

        [UnityTest]
        public IEnumerator ZeroRateAlarmIsSteadyAndConfigurationRejectsInvalidInputs()
        {
            var caption = NewText("AlarmCaption");
            var light = NewGraphic("AlarmLight");
            var alarmObject = NewObject("AlarmIndicator");
            var alarm = alarmObject.AddComponent<AlarmIndicator>();
            Assert.Throws<ArgumentNullException>(() => alarm.Configure(null, light));
            Assert.Throws<ArgumentNullException>(() => alarm.Configure(caption, null));
            Assert.Throws<ArgumentException>(() => alarm.Configure(caption, caption));
            Assert.Throws<ArgumentOutOfRangeException>(() => alarm.Configure(caption, light, 3.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => alarm.Configure(caption, light, float.NaN));
            alarm.Configure(caption, light, 0f);
            alarmObject.SetActive(true);
            Time.timeScale = 0f;

            alarm.SetAlarm(true, "Steady warning");
            Assert.IsTrue(light.enabled);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(light.enabled);
            Assert.AreEqual("Steady warning", caption.text);
        }

        [Test]
        public void DebriefShowsPassInstructionsAndHumanReadableIncorrectTarget()
        {
            LoadL7(out var passingResult, out _, out var mission);
            var panel = NewDebrief(out var outcome, out var details, out var heading, out var coach);

            panel.Show(passingResult, mission);
            Assert.AreEqual("PASSED", outcome.text);
            Assert.That(details.text, Does.Contain(FindInstruction(mission, "calibrate")));
            Assert.That(details.text, Does.Contain(FindInstruction(mission, "isolate_bus")));
            Assert.That(details.text, Does.Contain("Power Bus A"));
            Assert.That(details.text, Does.Contain("00:16"));
            Assert.IsFalse(heading.enabled);
            Assert.IsFalse(coach.enabled);
        }

        [Test]
        public void EmptyDebriefShowsRetryMissedCriticalInstructionsAndZeroTime()
        {
            LoadL7(out _, out var emptyResult, out var mission);
            var panel = NewDebrief(out var outcome, out var details, out _, out _);

            panel.Show(emptyResult, mission);
            Assert.AreEqual("NOT PASSED", outcome.text);
            Assert.That(details.text, Does.Contain("Required steps missed:"));
            Assert.That(details.text, Does.Contain(FindInstruction(mission, "calibrate")));
            Assert.That(details.text, Does.Contain(FindInstruction(mission, "verify_recovery")));
            Assert.That(details.text, Does.Contain("Time: 00:00"));
        }

        [Test]
        public void CoachTextIsAdvisoryAndClearedOnNextDebrief()
        {
            LoadL7(out var result, out _, out var mission);
            var panel = NewDebrief(out _, out _, out var heading, out var coach);

            panel.ShowCoachText("Review the isolation step.", isAi: true);
            Assert.AreEqual("AI coach (advisory)", heading.text);
            Assert.AreEqual("Review the isolation step.", coach.text);
            Assert.IsTrue(heading.enabled);
            Assert.IsTrue(coach.enabled);

            panel.Show(result, mission);
            Assert.AreEqual(string.Empty, heading.text);
            Assert.AreEqual(string.Empty, coach.text);
            Assert.IsFalse(heading.enabled);
            Assert.IsFalse(coach.enabled);
        }

        [Test]
        public void RetryAndBackToHubRequestsInvokeTheirUnityEvents()
        {
            var panel = NewDebrief(out _, out _, out _, out _);
            var retryCount = 0;
            var backCount = 0;
            panel.onRetry.AddListener(() => retryCount++);
            panel.onBackToHub.AddListener(() => backCount++);

            panel.RequestRetry();
            panel.RequestBackToHub();
            Assert.AreEqual(1, retryCount);
            Assert.AreEqual(1, backCount);
        }

        [Test]
        public void MismatchedMissionClearsStaleDisplayAndDoesNotMutateResult()
        {
            LoadL7(out var result, out _, out var mission);
            var panel = NewDebrief(out var outcome, out var details, out var heading, out var coach);
            panel.ShowCoachText("Old advice", isAi: true);
            panel.Show(result, mission);
            var before = JsonUtility.ToJson(result);
            var otherMission = new MissionDefinition { missionId = "l0-safety-v1", title = "Other mission" };

            LogAssert.Expect(LogType.Error,
                $"[DebriefPanel] {panel.gameObject.name}: result and mission are required and must have matching missionId values.");
            panel.Show(result, otherMission);

            Assert.AreEqual("Debrief unavailable", outcome.text);
            Assert.AreEqual(string.Empty, details.text);
            Assert.AreEqual(string.Empty, heading.text);
            Assert.AreEqual(string.Empty, coach.text);
            Assert.AreEqual(before, JsonUtility.ToJson(result));
        }

        private DebriefPanel NewDebrief(out Text outcome, out Text details, out Text heading, out Text coach)
        {
            outcome = NewText("Outcome");
            details = NewText("Details");
            heading = NewText("CoachHeading");
            coach = NewText("CoachAdvice");
            var panelObject = NewObject("DebriefPanel");
            var panel = panelObject.AddComponent<DebriefPanel>();
            panel.Configure(outcome, details, heading, coach);
            panelObject.SetActive(true);
            return panel;
        }

        private void LoadL7(out SessionResult passing, out SessionResult empty, out MissionDefinition mission)
        {
            var fixtures = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "fixtures"));
            var missions = Path.Combine(Application.dataPath, "ASTRA", "Content", "Missions");
            passing = JsonUtility.FromJson<SessionResult>(File.ReadAllText(Path.Combine(fixtures, "session-result.ok.json")));
            empty = JsonUtility.FromJson<SessionResult>(File.ReadAllText(Path.Combine(fixtures, "session-result.empty.json")));
            mission = JsonUtility.FromJson<MissionDefinition>(File.ReadAllText(Path.Combine(missions, "l7-restore-power-v1.json")));
        }

        private static string FindInstruction(MissionDefinition mission, string stepId)
        {
            foreach (var step in mission.steps)
                if (step.stepId == stepId)
                    return step.instruction;
            Assert.Fail($"Fixture mission is missing step '{stepId}'.");
            return string.Empty;
        }

        private Text NewText(string objectName)
        {
            var gameObject = NewObject(objectName, withRectTransform: true);
            gameObject.AddComponent<CanvasRenderer>();
            var text = gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Assert.IsNotNull(text.font, "LegacyRuntime.ttf is required by these legacy UGUI tests.");
            text.enabled = true;
            gameObject.SetActive(true);
            return text;
        }

        private Graphic NewGraphic(string objectName)
        {
            var gameObject = NewObject(objectName, withRectTransform: true);
            gameObject.AddComponent<CanvasRenderer>();
            var graphic = gameObject.AddComponent<Image>();
            gameObject.SetActive(true);
            return graphic;
        }

        private GameObject NewObject(string objectName, bool withRectTransform = false)
        {
            var gameObject = withRectTransform
                ? new GameObject(objectName, typeof(RectTransform))
                : new GameObject(objectName);
            gameObject.SetActive(false);
            objects.Add(gameObject);
            return gameObject;
        }
    }
}

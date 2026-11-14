using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Core.Progress;
using Astra.XR.Interactables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Astra.App.Tests.PlayMode
{
    public class LevelControllerTests
    {
        private const string L0 = "{\"missionId\":\"l0-safety-v1\",\"level\":0,\"version\":\"1.0.0\",\"title\":\"Safety\",\"fictional\":true,\"steps\":[" +
            "{\"stepId\":\"confirm_position\",\"order\":1,\"instruction\":\"Press CONFIRM POSITION.\",\"hint\":\"h1\",\"expectedEvent\":\"calibration_completed\",\"targetId\":\"calibration\",\"critical\":true}," +
            "{\"stepId\":\"open_pause\",\"order\":2,\"instruction\":\"Press PAUSE.\",\"hint\":\"h2\",\"expectedEvent\":\"paused\",\"targetId\":\"system\",\"critical\":true}," +
            "{\"stepId\":\"recenter_view\",\"order\":3,\"instruction\":\"Press RECENTER.\",\"hint\":\"h3\",\"expectedEvent\":\"recentered\",\"targetId\":\"system\",\"critical\":true}," +
            "{\"stepId\":\"resume_training\",\"order\":4,\"instruction\":\"Press RESUME.\",\"hint\":\"h4\",\"expectedEvent\":\"resumed\",\"targetId\":\"system\",\"critical\":true}]}";

        private GameObject host;
        private string directory;
        private readonly List<ModulePhase> phases = new List<ModulePhase>();
        private readonly List<SessionResult> results = new List<SessionResult>();

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "astra-level-tests-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            phases.Clear();
            results.Clear();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator PracticeThenQualificationScoresSavesProgressAndExcludesPausedTime()
        {
            var controller = Create();
            yield return WaitFor(() => controller.Phase == ModulePhase.Practice);
            Assert.That(phases, Is.EqualTo(new[] { ModulePhase.Briefing, ModulePhase.Practice }));

            Raise(EventNames.ControlActivated, "orientation_start_button"); // wrong action, recorded incorrect
            CompleteSafetySequence(controller, pauseSeconds: 0f);
            yield return null;
            Assert.That(controller.Runner.IsComplete, Is.True);
            yield return WaitFor(() => controller.Phase == ModulePhase.Qualification, 5f);

            Raise(EventNames.CalibrationCompleted, SystemTargets.Calibration);
            Raise(EventNames.Paused, SystemTargets.System);
            Assert.That(controller.IsAttemptPaused, Is.True);
            yield return new WaitForSecondsRealtime(0.4f);
            Raise(EventNames.ControlActivated, "orientation_start_button"); // suppressed while paused
            Raise(EventNames.Recentered, SystemTargets.System);
            var events = controller.Runner.Events;
            Assert.That(events.Count, Is.EqualTo(3));
            Assert.That(events[2].timestampMs - events[1].timestampMs, Is.LessThan(50), "paused time is excluded");
            Raise(EventNames.Resumed, SystemTargets.System);

            Assert.That(controller.Phase, Is.EqualTo(ModulePhase.Debrief));
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].passed, Is.True);
            Assert.That(results[0].incorrectActions, Is.Empty);

            var saved = JsonUtility.FromJson<ProgressState>(File.ReadAllText(Path.Combine(directory, "progress.json")));
            Assert.That(saved.unlockedLevels, Does.Contain(1));
            Assert.That(saved.records[0].passed, Is.True);
            Assert.That(Directory.GetFiles(Path.Combine(directory, "SessionLogs")), Has.Length.EqualTo(1));

            controller.Retry();
            Assert.That(controller.Phase, Is.EqualTo(ModulePhase.Practice));
            Assert.That(controller.Runner.Events, Is.Empty);
        }

        [UnityTest]
        public IEnumerator FinishingQualificationEarlyFailsWithoutUnlocking()
        {
            var controller = Create();
            yield return WaitFor(() => controller.Phase == ModulePhase.Practice);
            CompleteSafetySequence(controller, 0f);
            yield return WaitFor(() => controller.Phase == ModulePhase.Qualification, 5f);
            Raise(EventNames.CalibrationCompleted, SystemTargets.Calibration);
            controller.FinishAttempt();
            Assert.That(controller.Phase, Is.EqualTo(ModulePhase.Debrief));
            Assert.That(results[0].passed, Is.False);
            Assert.That(results[0].omittedCriticalSteps, Is.EqualTo(new[] { "open_pause", "recenter_view", "resume_training" }));
            var saved = JsonUtility.FromJson<ProgressState>(File.ReadAllText(Path.Combine(directory, "progress.json")));
            Assert.That(saved.unlockedLevels, Is.EqualTo(new[] { 0 }));

            controller.RestartModule();
            Assert.That(controller.Phase, Is.EqualTo(ModulePhase.Briefing));
        }

        [UnityTest]
        public IEnumerator InteractionsOutsideAttemptsAreNotRecorded()
        {
            var controller = Create(briefing: 30f);
            yield return null;
            Assert.That(controller.Phase, Is.EqualTo(ModulePhase.Briefing));
            Raise(EventNames.CalibrationCompleted, SystemTargets.Calibration);
            Assert.That(controller.Runner, Is.Null);
        }

        private static void CompleteSafetySequence(LevelController controller, float pauseSeconds)
        {
            Raise(EventNames.CalibrationCompleted, SystemTargets.Calibration);
            Raise(EventNames.Paused, SystemTargets.System);
            Raise(EventNames.Recentered, SystemTargets.System);
            Raise(EventNames.Resumed, SystemTargets.System);
        }

        private static void Raise(string eventName, string target) =>
            InteractionEvents.Raise(eventName, target, InputModality.Simulator);

        private LevelController Create(float briefing = 0f)
        {
            host = new GameObject("level");
            host.SetActive(false);
            var controller = host.AddComponent<LevelController>();
            Set(controller, "missionJson", new TextAsset(L0));
            Set(controller, "briefingSeconds", briefing);
            Set(controller, "demoSecondsPerStep", 0f);
            controller.DataDirectory = directory;
            controller.SceneAvailable = scene => scene == "L0_Safety" || scene == "L1_Orientation";
            controller.LoadScene = _ => { };
            controller.PhaseChanged += phases.Add;
            controller.QualificationScored += results.Add;
            host.SetActive(true);
            return controller;
        }

        private static void Set(object target, string field, object value) =>
            typeof(LevelController).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeout = 2f)
        {
            var end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end)
                yield return null;
            Assert.That(condition(), Is.True, "condition not reached before timeout");
        }
    }
}

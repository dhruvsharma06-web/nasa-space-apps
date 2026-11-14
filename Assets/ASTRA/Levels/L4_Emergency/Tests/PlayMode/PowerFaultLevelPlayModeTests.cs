using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Astra.App;
using Astra.Core.Mission;
using Astra.Core.Progress;
using Astra.DesktopTest;
using Astra.UI;
using Astra.UI.Progress;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Astra.Levels.Tests.PlayMode
{
    /// <summary>
    /// The generated L4 and L7 scenes with the real LevelController (TASKS S5 + Task 5 hardening):
    /// the fault drama, pausing in every phase, skip/retry/finish spam, a double press, the wrong bus,
    /// and the score and progress that come out. Any logged error or exception fails the test.
    /// Editor PlayMode evidence only, not phone evidence. Scenes are loaded by path, so they need not be in Build Settings.
    /// </summary>
    [TestFixture("Assets/ASTRA/Levels/L4_Emergency/L4_Emergency.unity", "power_bus_b_dial", "power_bus_a_dial", "crosstie_lever")]
    [TestFixture("Assets/ASTRA/Levels/L7_Finale/L7_Finale.unity", "power_bus_b_selector", "power_bus_a_selector", "crosstie_breaker")]
    public class PowerFaultLevelPlayModeTests
    {
        private const string AckId = "caution_ack_button";
        private const float DimScale = 0.35f;

        private readonly string _scenePath, _isolateId, _wrongDialId, _crossTieId;
        private string _saved;
        private LevelController _level;
        private DesktopHud _hud;

        public PowerFaultLevelPlayModeTests(string scenePath, string isolateId, string wrongDialId, string crossTieId)
        {
            _scenePath = scenePath;
            _isolateId = isolateId;
            _wrongDialId = wrongDialId;
            _crossTieId = crossTieId;
        }

        [UnitySetUp]
        public IEnumerator LoadLevel()
        {
            _saved = null;
            ProgressService.Initialize(new KeyValueProgressStore(() => _saved, s => _saved = s,
                s => JsonUtility.ToJson(s), JsonUtility.FromJson<ProgressState>));
            AcademyProgress.Current = new CoreAcademyProgress();
            ScoreReports.Source = new ResultRatingScoreSource();
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(_scenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("needs the Editor to load a scene by path");
#endif
            for (int f = 0; f < 3; f++) yield return null;
            _level = Object.FindAnyObjectByType<LevelController>();
            _hud = Object.FindAnyObjectByType<DesktopHud>();
            Assert.IsNotNull(_level, "LevelController");
            Assert.IsNotNull(_hud, "DesktopHud");
            Assert.IsNotNull(Alarm, "AlarmIndicator");
            _hud.StartMission();
            yield return null;
            Assert.AreEqual(ModulePhase.Briefing, _level.Phase);
        }

        // ------------------------------------------------------------------ the drama

        [UnityTest]
        public IEnumerator AlarmAndDimmedLightsLastFromTheFaultUntilTheCrossTie()
        {
            Assert.IsFalse(Alarm.IsAlarmActive, "no alarm in the briefing");
            var lights = CabinLights();
            Assert.IsNotEmpty(lights, "Cabin_LED lights");
            var full = lights.Select(l => l.intensity).ToArray();

            _level.SkipToPractice();
            yield return null;
            Assert.AreEqual(ModulePhase.Practice, _level.Phase);
            Assert.IsTrue(Alarm.IsAlarmActive, "the fault starts the alarm");
            Assert.AreEqual("STATUS: FAULT", Label("status_light"));
            Assert.AreEqual("FAULT", Label("bus_b_light"));
            yield return new WaitForSecondsRealtime(1.2f);
            AssertLights(lights, full, DimScale, "dimmed during the fault");

            yield return PlayStepsUntil(AckId);
            Assert.IsTrue(Alarm.IsAlarmActive, "ACK confirms the alarm; it does not clear the fault");
            yield return PlayStepsUntil(_isolateId);
            Assert.IsTrue(Alarm.IsAlarmActive, "isolating does not restore power yet");
            Assert.AreEqual("ISOLATED", Label("bus_b_light"));
            yield return PlayStepsUntil(_crossTieId);
            Assert.IsFalse(Alarm.IsAlarmActive, "the cross-tie restores power");
            Assert.AreEqual("STATUS: RESTORED", Label("status_light"));
            yield return new WaitForSecondsRealtime(1.2f);
            AssertLights(lights, full, 1f, "back to full after the cross-tie");
        }

        // ------------------------------------------------------------------ hardening

        [UnityTest]
        public IEnumerator PauseAndResumeInEveryPhaseAreSafe()
        {
            yield return PauseResume(ModulePhase.Briefing);
            _level.Skip();
            yield return null;
            yield return PauseResume(ModulePhase.Demonstration);
            _level.Skip();
            yield return null;
            yield return PauseResume(ModulePhase.Practice);
            yield return PlayStepsUntil(null);
            yield return WaitForPhase(ModulePhase.Qualification);
            yield return PauseResume(ModulePhase.Qualification);
            _level.Finish();
            yield return null;
            yield return PauseResume(ModulePhase.Debrief);
        }

        [UnityTest]
        public IEnumerator QualificationWithPauseDoublePressAndTheWrongBusScoresExactly()
        {
            _level.SkipToPractice();
            yield return null;
            yield return PlayStepsUntil(null);
            yield return WaitForPhase(ModulePhase.Qualification);

            bool wrongDone = false, paused = false;
            for (int guard = 0; guard < 40 && _level.Runner?.CurrentStep != null; guard++)
            {
                var step = _level.Runner.CurrentStep;
                if (step.targetId == _isolateId && !wrongDone)
                {
                    yield return Press(_wrongDialId);   // the wrong bus first
                    wrongDone = true;
                    Assert.IsTrue(Alarm.IsAlarmActive, "the wrong bus leaves the fault in place");
                    Assert.AreEqual("STATUS: FAULT", Label("status_light"));
                }
                if (step.order == 3 && !paused)
                {
                    _level.Pause(); yield return null; _level.Resume(); yield return null;
                    paused = true;
                }
                yield return Press(step.targetId);
                if (step.targetId == AckId) yield return Press(AckId);   // double press
            }
            yield return WaitForPhase(ModulePhase.Debrief);

            var result = _level.LastResult;
            Assert.IsTrue(result.passed);
            CollectionAssert.AreEqual(new[] { _wrongDialId }, result.incorrectActions);
            Assert.IsEmpty(result.sequenceErrors);
            var report = ScoreReports.Build(result, _level.Mission);
            Assert.AreEqual(92, report.Score);
            Assert.AreEqual(Medal.Gold, report.Medal);

            yield return null;   // progress is recorded one frame after scoring
            yield return null;
            int level = _level.Mission.level;
            Assert.AreEqual(92, ProgressService.BestScore(level));
            Assert.AreEqual(1, ProgressService.Get(level).attempts);
            Assert.AreEqual(Medal.Gold, AcademyProgress.Current.BestMedal(level));
            if (level < 7) Assert.IsTrue(ProgressService.IsUnlocked(level + 1), "passing unlocks the next level");
        }

        [UnityTest]
        public IEnumerator SkipFinishAndRetrySpamAreSafeAndRetryStartsAFreshFault()
        {
            for (int i = 0; i < 25; i++) { _level.Skip(); _level.SkipToPractice(); }
            yield return null;
            Assert.AreEqual(ModulePhase.Practice, _level.Phase);
            for (int i = 0; i < 10; i++) _level.Finish();   // not allowed in Practice
            yield return null;
            Assert.AreEqual(ModulePhase.Practice, _level.Phase);

            yield return PlayStepsUntil(null);
            yield return WaitForPhase(ModulePhase.Qualification);
            _level.Finish();   // gives up straight away
            _level.Finish();
            yield return null;
            Assert.AreEqual(ModulePhase.Debrief, _level.Phase);
            Assert.IsFalse(_level.LastResult.passed);
            var report = ScoreReports.Build(_level.LastResult, _level.Mission);
            Assert.LessOrEqual(report.Score, 59);
            Assert.AreEqual(Medal.None, report.Medal);
            yield return null;
            yield return null;
            Assert.AreEqual(1, ProgressService.Get(_level.Mission.level).attempts, "one debrief, one attempt");

            for (int i = 0; i < 5; i++) _level.Retry();
            yield return null;
            Assert.AreEqual(ModulePhase.Practice, _level.Phase);
            Assert.IsTrue(Alarm.IsAlarmActive, "a retry starts a fresh fault");
            Assert.AreEqual("STATUS: FAULT", Label("status_light"));
            Assert.AreEqual(_level.Mission.steps.Min(s => s.order), _level.Runner.CurrentStep.order, "a retry starts from step 1");
        }

        // ------------------------------------------------------------------ helpers

        private static AlarmIndicator Alarm => Object.FindAnyObjectByType<AlarmIndicator>();

        private static List<Light> CabinLights() =>
            Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.name == "Cabin_LED").ToList();

        private static string Label(string indicatorId)
        {
            var go = GameObject.Find(indicatorId + "_Label");
            return go != null && go.TryGetComponent<TMP_Text>(out var t) ? t.text : null;
        }

        private static void AssertLights(List<Light> lights, float[] full, float scale, string what)
        {
            for (int i = 0; i < lights.Count; i++)
                Assert.AreEqual(full[i] * scale, lights[i].intensity, 0.02f * Mathf.Max(1f, full[i]), $"{what}: {lights[i].name} #{i}");
        }

        private static IEnumerator Press(string targetId)
        {
            Assert.IsTrue(TestTarget.Registry.TryGetValue(targetId, out var target), $"no control '{targetId}' in the scene");
            target.Activate();
            yield return null;
        }

        /// <summary>Presses the current step's control until <paramref name="stopAfter"/> is pressed (null = every step).</summary>
        private IEnumerator PlayStepsUntil(string stopAfter)
        {
            for (int guard = 0; guard < 40; guard++)
            {
                var step = _level.Runner?.CurrentStep;
                if (step == null) yield break;
                yield return Press(step.targetId);
                Assert.AreNotEqual(step.stepId, _level.Runner?.CurrentStep?.stepId, $"pressing '{step.targetId}' did not complete '{step.stepId}'");
                if (step.targetId == stopAfter) yield break;
            }
            Assert.Fail("steps never finished");
        }

        private IEnumerator WaitForPhase(ModulePhase phase)
        {
            float until = Time.realtimeSinceStartup + 10f;
            while (_level.Phase != phase && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(phase, _level.Phase, "phase");
        }

        private IEnumerator PauseResume(ModulePhase phase)
        {
            Assert.AreEqual(phase, _level.Phase, "before pausing");
            _level.Pause();
            _level.Pause();
            yield return null;
            Assert.IsTrue(_level.IsPaused);
            Assert.AreEqual(phase, _level.Phase, "pausing never changes the phase");
            yield return new WaitForSecondsRealtime(0.2f);
            _level.Resume();
            _level.Resume();
            yield return null;
            Assert.IsFalse(_level.IsPaused);
            Assert.AreEqual(phase, _level.Phase, "resuming never changes the phase");
        }
    }
}

using System.Collections;
using System.IO;
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

namespace Astra.Levels.Launch.Tests.PlayMode
{
    /// <summary>
    /// L8 Launch Day with the real LevelController and the generated scene, checked through the objects that
    /// <c>L8LaunchView</c> drives:
    /// <list type="bullet">
    /// <item>the pad at sunrise;</item>
    /// <item>the countdown, ignition and liftoff (the tower drops away);</item>
    /// <item>the climb, staging, the escape-tower jettison, orbit (the Earth appears) and weightlessness;</item>
    /// <item>a clean qualification passes and ends in orbit.</item>
    /// </list>
    /// Saves window shots to Builds/Shots/L8_*.png. L8_Launch_front.png is what the Hub builder imports as the L8 card preview.
    /// Any logged error fails the test. Editor PlayMode evidence only, not phone evidence.
    /// </summary>
    public class LaunchLevelPlayModeTests
    {
        private const string ScenePath = "Assets/ASTRA/Levels/L8_Launch/L8_Launch.unity";

        private string _saved;
        private LevelController _level;
        private DesktopHud _hud;

        [UnitySetUp]
        public IEnumerator LoadLevel()
        {
            _saved = null;
            ProgressService.Initialize(new KeyValueProgressStore(() => _saved, s => _saved = s,
                s => JsonUtility.ToJson(s), JsonUtility.FromJson<ProgressState>));
            AcademyProgress.Current = new CoreAcademyProgress();
            ScoreReports.Source = new ResultRatingScoreSource();
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("needs the Editor to load a scene by path");
#endif
            for (int f = 0; f < 3; f++) yield return null;
            _level = Object.FindAnyObjectByType<LevelController>();
            _hud = Object.FindAnyObjectByType<DesktopHud>();
            Assert.IsNotNull(_level, "LevelController");
            Assert.IsNotNull(_hud, "DesktopHud");
            Assert.AreEqual(11, _level.Mission.steps.Count, "eleven launch steps");
            _hud.StartMission();
            yield return null;
            Assert.AreEqual(ModulePhase.Briefing, _level.Phase);
        }

        [UnityTest]
        public IEnumerator TheLaunchFliesFromThePadToOrbit()
        {
            var tower = GameObject.Find("Launch_Tower");
            Assert.IsNotNull(tower, "the launch tower is in the window");
            Assert.IsNotNull(GameObject.Find("Launch_Sky"), "the sunrise sky");
            var towerY = tower.transform.position.y;
            var earth = EarthRenderer();
            Assert.IsFalse(earth.enabled, "no Earth from orbit while on the pad");

            _level.SkipToPractice();
            yield return null;
            yield return new WaitForSecondsRealtime(1f);
            Shot("L8_0_pad");

            yield return PressNext();   // HARNESS
            yield return PressNext();   // ENGINE ARM
            yield return PressNext();   // GUIDANCE
            yield return PressNext();   // LAUNCH
            StringAssert.Contains("T-00:", Telemetry(), "the countdown runs");
            yield return new WaitForSecondsRealtime(5.5f);
            StringAssert.Contains("STAGE 1", Telemetry(), "ignition at T-6");
            Shot("L8_1_ignition");

            yield return new WaitForSecondsRealtime(5.5f);
            StringAssert.Contains("T+", Telemetry(), "liftoff at T-0");
            Assert.Less(tower.transform.position.y, towerY - 5f, "the tower drops away");
            Shot("L8_2_liftoff");

            yield return new WaitForSecondsRealtime(4f);
            Shot("L8_3_clouds");
            yield return PressNext();   // THROTTLE 70%
            yield return new WaitForSecondsRealtime(9f);
            Assert.AreEqual("BOOSTER SEP", Label("booster_sep_light"), "booster burnout");
            Shot("L8_4_booster_sep");

            yield return PressNext();   // STAGE
            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual("SEP DONE", Label("booster_sep_light"), "staged");
            Shot("L8_5_staging");

            // the emergency: a fire breaks out on AUX POWER a few seconds into the second-stage burn
            yield return new WaitForSecondsRealtime(3f);
            var fire = GameObject.Find("Cabin_Fire").GetComponent<ParticleSystem>();
            var alarmLight = GameObject.Find("Alarm_Light").GetComponent<Light>();
            Assert.Greater(fire.particleCount, 0, "flames on AUX POWER");
            StringAssert.Contains("ALARM: FIRE", Telemetry(), "the alarm is on");
            Shot("L8_5a_fire", FirePoint());
            Shot("L8_5b_fire_cabin");
            var extinguisher = GameObject.Find("fire_extinguisher");
            Assert.IsNotNull(extinguisher.transform.Find("Ext_Nozzle_Tip"), "the tool is built as an extinguisher with a nozzle");
            Shot("L8_5d_extinguisher", extinguisher.transform.position + Vector3.up * 0.05f);
            var home = extinguisher.transform.position;
            yield return PressNext();   // grab the FIRE EXT
            yield return PressNext();   // spray the FIRE
            yield return new WaitForSecondsRealtime(0.9f);
            Assert.AreEqual(Camera.main.transform, extinguisher.transform.parent, "the extinguisher is sprayed from the cadet's hand");
            Assert.Greater(GameObject.Find("CO2_Spray").GetComponent<ParticleSystem>().particleCount, 0, "CO2 comes out of the nozzle");
            Shot("L8_5c_co2", FirePoint());
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.AreEqual(0, fire.emission.rateOverTime.constant, 0.001f, "the flames are out");
            Assert.AreEqual(0f, alarmLight.intensity, 0.001f, "the alarm light is off");
            Assert.IsFalse(Telemetry().Contains("FIRE"), "the alarm is cleared");
            yield return new WaitForSecondsRealtime(1f);
            Assert.Less(Vector3.Distance(home, extinguisher.transform.position), 0.01f, "the extinguisher goes back on its bracket");
            yield return new WaitForSecondsRealtime(3f);

            yield return PressNext();   // TOWER JETT
            yield return new WaitForSecondsRealtime(0.6f);
            Shot("L8_6_jettison");

            float until = Time.realtimeSinceStartup + 25f;
            while (!earth.enabled && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(earth.enabled, "the Earth comes into view near orbit");
            yield return new WaitForSecondsRealtime(6f);
            Shot("L8_7_orbit");

            yield return PressNext();   // CUTOFF
            yield return new WaitForSecondsRealtime(4f);
            StringAssert.Contains("COAST", Telemetry(), "engine off");
            Shot("L8_8_weightless");
            Shot("L8_Launch_front");   // the Hub card preview: the Earth in the windshield

            yield return PressNext();   // REPORT
            yield return WaitForPhase(ModulePhase.Qualification);
            yield return null;
            yield return null;
            Assert.IsFalse(EarthRenderer().enabled, "Qualification starts back on the pad");
            // a later flight gets its clouds and pad steam back (their roots were switched off during the climb)
            Assert.Greater(GameObject.Find("Clouds_High").GetComponent<ParticleSystem>().particleCount, 0, "the sunrise clouds are back");
            Assert.IsTrue(GameObject.Find("Pad_Vent").GetComponent<ParticleSystem>().isPlaying, "the pad steam is back");
            Assert.AreEqual(0, GameObject.Find("Cabin_Fire").GetComponent<ParticleSystem>().particleCount, "no fire on the pad");
            yield return new WaitForSecondsRealtime(1f);
            Shot("L8_9_qualification_pad");
        }

        [UnityTest]
        public IEnumerator AFastCleanQualificationPassesAndEndsInOrbit()
        {
            // the demonstration is the first launch a player sees: let it run a while and keep a shot
            float demoUntil = Time.realtimeSinceStartup + 10f;
            while (_level.Phase != ModulePhase.Demonstration && Time.realtimeSinceStartup < demoUntil) yield return null;
            Assert.AreEqual(ModulePhase.Demonstration, _level.Phase, "the briefing leads into the demonstration");
            yield return new WaitForSecondsRealtime(12f);
            Shot("L8_demo");
            _level.SkipToPractice();
            yield return null;
            yield return PlayStepsUntil(null);
            yield return WaitForPhase(ModulePhase.Qualification);
            yield return PlayStepsUntil(null);
            yield return WaitForPhase(ModulePhase.Debrief);
            Assert.IsTrue(_level.LastResult.passed, "a clean qualification passes");
            var report = ScoreReports.Build(_level.LastResult, _level.Mission);
            Assert.GreaterOrEqual(report.Score, 90, "and scores Gold");
            float until = Time.realtimeSinceStartup + 25f;
            while (!EarthRenderer().enabled && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(EarthRenderer().enabled, "the flight catches up to orbit in the debrief");
        }

        // ------------------------------------------------------------------ helpers

        private static Renderer EarthRenderer()
        {
            var space = GameObject.Find("Space_View");
            Assert.IsNotNull(space, "Space_View");
            var earth = space.transform.Find("Earth");
            Assert.IsNotNull(earth, "Space_View/Earth");
            return earth.GetComponent<Renderer>();
        }

        private static string Telemetry()
        {
            var go = GameObject.Find("Flight_Telemetry");
            Assert.IsNotNull(go, "Flight_Telemetry");
            return go.GetComponent<TMP_Text>().text;
        }

        private static string Label(string indicatorId)
        {
            var go = GameObject.Find(indicatorId + "_Label");
            return go != null && go.TryGetComponent<TMP_Text>(out var t) ? t.text : null;
        }

        private IEnumerator PressNext()
        {
            var step = _level.Runner?.CurrentStep;
            Assert.IsNotNull(step, "a step to press");
            Assert.IsTrue(TestTarget.Registry.TryGetValue(step.targetId, out var target), $"no control '{step.targetId}' in the scene");
            target.Activate();
            yield return null;
            Assert.AreNotEqual(step.stepId, _level.Runner?.CurrentStep?.stepId, $"pressing '{step.targetId}' did not complete '{step.stepId}'");
            yield return new WaitForSecondsRealtime(0.4f);
        }

        private IEnumerator PlayStepsUntil(string stopAfter)
        {
            for (int guard = 0; guard < 40; guard++)
            {
                var step = _level.Runner?.CurrentStep;
                if (step == null) yield break;
                Assert.IsTrue(TestTarget.Registry.TryGetValue(step.targetId, out var target), $"no control '{step.targetId}' in the scene");
                target.Activate();
                yield return null;
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

        private static Vector3 FirePoint()
        {
            var zone = GameObject.Find("fire_zone");
            return zone != null ? zone.transform.position + Vector3.up * 0.12f : new Vector3(0f, 0.88f, 0.5f);
        }

        /// <summary>Renders the view out of the window (eye to window centre), or toward <paramref name="lookAt"/>, to Builds/Shots.</summary>
        private static void Shot(string name, Vector3? lookAt = null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rot = cam.transform.rotation;
            cam.transform.rotation = Quaternion.LookRotation((lookAt ?? new Vector3(0f, 1.72f, 1.03f)) - cam.transform.position);
            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            cam.transform.rotation = rot;
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory("Builds/Shots");
            File.WriteAllBytes($"Builds/Shots/{name}.png", tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}

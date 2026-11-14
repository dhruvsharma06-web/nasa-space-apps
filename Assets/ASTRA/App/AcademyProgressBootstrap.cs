using System.Collections;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Core.Progress;
using Astra.UI.Hub;
using Astra.UI.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Astra.App
{
    /// <summary>
    /// Connects Core's progress and scoring to the app at startup (TASKS S6/S7, OWNER: Core Logic, in App by agreement):
    /// <list type="bullet">
    /// <item>picks the progress store: files on the phone, one PlayerPrefs key in the browser (WebGL files are not kept
    /// across a reload) and in the Editor (kept apart from progress.json so tests never see a developer's unlocks);</item>
    /// <item>supplies <see cref="AcademyProgress.Current"/> (Hub, certificate) and <see cref="ScoreReports.Source"/>
    /// (debrief) from <see cref="ProgressService"/> and <see cref="ResultRating"/>, replacing P1's placeholders;</item>
    /// <item>records every scored Qualification of every level through <see cref="ProgressService.Record"/>, one frame
    /// after scoring, so the debrief can compare against the previous best first (NEW BEST).</item>
    /// </list>
    /// It only reads <see cref="LevelController.QualificationScored"/>; it never changes a result (CONTRACT §5).
    /// </summary>
    public sealed class AcademyProgressBootstrap : MonoBehaviour
    {
        public const string PlayerPrefsKey = "astra.progress.v1";
        public const string EditorPlayerPrefsKey = "astra.progress.editor.v1";

        private static AcademyProgressBootstrap _instance;
        private readonly Dictionary<LevelController, System.Action<SessionResult>> _hooked =
            new Dictionary<LevelController, System.Action<SessionResult>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            ProgressService.Initialize(CreateStore(), AvailableLevels());
            AcademyProgress.Current = new CoreAcademyProgress();
            ScoreReports.Source = new ResultRatingScoreSource();

            if (_instance != null) return;
            var go = new GameObject("AcademyProgress") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AcademyProgressBootstrap>();
        }

        /// <summary>The store for this platform. Never null.</summary>
        public static IProgressStore CreateStore()
        {
            if (Application.isEditor) return PlayerPrefsStore(EditorPlayerPrefsKey);
            if (Application.platform == RuntimePlatform.WebGLPlayer) return PlayerPrefsStore(PlayerPrefsKey);
            return new FileProgressStore(Application.persistentDataPath, s => JsonUtility.ToJson(s, true),
                JsonUtility.FromJson<ProgressState>);
        }

        private static IProgressStore PlayerPrefsStore(string key) => new KeyValueProgressStore(
            () => PlayerPrefs.GetString(key, string.Empty),
            json => { PlayerPrefs.SetString(key, json); PlayerPrefs.Save(); },
            s => JsonUtility.ToJson(s),
            JsonUtility.FromJson<ProgressState>);

        /// <summary>Levels whose scene is in the build, so passing a level skips over a missing one.</summary>
        private static List<int> AvailableLevels()
        {
            var levels = new List<int>();
            foreach (var entry in LevelCatalog.Levels)
                if (entry.Level == 0 || Application.CanStreamedLevelBeLoaded(entry.SceneName))
                    levels.Add(entry.Level);
            return levels;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            HookLevels();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            foreach (var pair in _hooked)
                if (pair.Key != null) pair.Key.QualificationScored -= pair.Value;
            _hooked.Clear();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HookLevels();

        private void HookLevels()
        {
            var gone = new List<LevelController>();
            foreach (var lc in _hooked.Keys) if (lc == null) gone.Add(lc);
            foreach (var lc in gone) _hooked.Remove(lc);

            foreach (var lc in FindObjectsByType<LevelController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (_hooked.ContainsKey(lc)) continue;
                var level = lc;
                // The mission is taken when the score arrives, so a scene change during the next frame loses nothing.
                System.Action<SessionResult> handler = result => StartCoroutine(RecordNextFrame(level.Mission, result));
                _hooked[lc] = handler;
                lc.QualificationScored += handler;
            }
        }

        private static IEnumerator RecordNextFrame(MissionDefinition mission, SessionResult result)
        {
            yield return null;
            if (mission == null || result == null) yield break;
            ProgressService.Record(mission.level, result, mission);
            if (ProgressService.LastSaveError != null)
                Debug.LogWarning("[AcademyProgress] progress was not saved: " + ProgressService.LastSaveError);
        }
    }

    /// <summary>P1's <see cref="IAcademyProgress"/> over <see cref="ProgressService"/>.</summary>
    public sealed class CoreAcademyProgress : IAcademyProgress
    {
        public bool IsUnlocked(int level) => ProgressService.IsUnlocked(level);
        public bool IsPassed(int level) => ProgressService.IsPassed(level);
        public int BestScore(int level) => ProgressService.BestScore(level);
        public Medal BestMedal(int level) => (Medal)(int)ProgressService.BestRating(level);
        public int TotalScore => ProgressService.AcademyScore;

        public string Callsign
        {
            get
            {
                var c = ProgressService.Callsign;
                return string.IsNullOrWhiteSpace(c) ? "CADET" : c.ToUpperInvariant();
            }
        }

        public bool AllUnlocked => ProgressService.DemoUnlockAll;
        public void UnlockAll() => ProgressService.DemoUnlockAll = true;

        public bool RecordScore(int level, int score, Medal medal, bool passed) =>
            ProgressService.RecordScore(level, score, (Rating)(int)medal, passed);
    }

    /// <summary>P1's <see cref="IScoreReportSource"/> over <see cref="ResultRating"/> (docs/SCORING.md).</summary>
    public sealed class ResultRatingScoreSource : IScoreReportSource
    {
        public ScoreReport Build(SessionResult result, MissionDefinition mission)
        {
            var report = new ScoreReport();
            if (result == null || mission == null) return report;
            var b = ResultRating.From(result, mission);
            report.Score = b.Score;
            report.Medal = (Medal)(int)b.Rating;
            report.Passed = result.passed;
            foreach (var (label, points) in b.Lines)
                report.Lines.Add(new BreakdownLine(label, points));
            return report;
        }
    }
}

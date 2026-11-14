using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Core.Progress;
using Astra.Integrations.Coach;
using Astra.UI;
using Astra.UI.Hub;
using Astra.XR.Interactables;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Astra.App
{
    /// <summary>
    /// One per level scene. Loads the mission, runs the phases, forwards InteractionEvents to the
    /// mission runner, scores Qualification, shows the debrief, saves progress (CONTRACT.md §8).
    /// OWNER: Integration Lead. Implemented by P1 against CONTRACT.md §8 for the L0/L1 desktop build;
    /// handed off to the Integration Lead for review. Public surface is fixed by the contract.
    /// Level-specific scripts may subscribe to PhaseChanged / EventRecorded but must not drive the runner.
    /// </summary>
    public class LevelController : MonoBehaviour
    {
        public const string HubSceneName = "Hub";
        private const double PracticeToQualificationSeconds = 3.0;

        [SerializeField] private TextAsset missionJson;
        [SerializeField] private CoachProvider coachProvider;
        [SerializeField] private float briefingSeconds = 6f;
        [SerializeField] private float demoSecondsPerStep = 4f;

        [Header("Scene UI (CONTRACT.md §7)")]
        [SerializeField] private InstructionPanel instructionPanel;
        [SerializeField] private PhaseBanner phaseBanner;
        [SerializeField] private DebriefPanel debriefPanel;
        [SerializeField] private PauseMenu pauseMenu;

        public event Action<ModulePhase> PhaseChanged;
        public event Action<SessionEvent, StepFeedback> EventRecorded;
        public event Action<SessionResult> QualificationScored;

        public MissionDefinition Mission { get; private set; }
        public ModulePhase Phase { get; private set; } = ModulePhase.Briefing;

        private readonly ModulePhaseMachine machine = new ModulePhaseMachine();
        private readonly IMissionScorer scorer = new MissionScorer();
        private IMissionRunner runner;
        private double attemptSeconds;
        private double phaseTimer;
        private int demoIndex;
        private bool attemptPaused;
        private bool practiceComplete;
        private bool subscribed;
        private bool started;
        private bool leaving;
        private bool reporting;

        // Composition hooks (tests and alternative hosts). Defaults follow CONTRACT.md §6 and §8.
        internal string DataDirectory { get; set; }
        internal Func<string, bool> SceneAvailable { get; set; } = Application.CanStreamedLevelBeLoaded;
        internal Action<string> LoadScene { get; set; } = name => SceneManager.LoadScene(name);
        internal IMissionRunner Runner => runner;
        internal bool IsAttemptPaused => attemptPaused;
        internal SessionResult LastResult { get; private set; }

        protected virtual void Awake()
        {
            if (missionJson == null)
            {
                Debug.LogError($"[LevelController] {name}: assign the mission JSON.", this);
                enabled = false;
                return;
            }
            try
            {
                Mission = JsonUtility.FromJson<MissionDefinition>(missionJson.text);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            if (Mission == null || Mission.steps == null || Mission.steps.Count == 0 || string.IsNullOrEmpty(Mission.missionId))
            {
                Debug.LogError($"[LevelController] {name}: mission JSON could not be read.", this);
                Mission = null;
                enabled = false;
                return;
            }
            Mission.steps.Sort((a, b) => a.order.CompareTo(b.order));
        }

        protected virtual void OnEnable()
        {
            if (Mission == null)
                return;
            InteractionEvents.Reported += HandleReport;
            if (pauseMenu != null)
            {
                pauseMenu.onPause.AddListener(HandlePause);
                pauseMenu.onResume.AddListener(HandleResume);
                pauseMenu.onRestart.AddListener(RestartModule);
                pauseMenu.onExit.AddListener(ExitToHub);
                pauseMenu.onFinish.AddListener(FinishAttempt);
            }
            if (debriefPanel != null)
            {
                debriefPanel.onRetry.AddListener(Retry);
                debriefPanel.onBackToHub.AddListener(ExitToHub);
            }
            subscribed = true;
        }

        protected virtual void OnDisable()
        {
            if (!subscribed)
                return;
            InteractionEvents.Reported -= HandleReport;
            if (pauseMenu != null)
            {
                pauseMenu.onPause.RemoveListener(HandlePause);
                pauseMenu.onResume.RemoveListener(HandleResume);
                pauseMenu.onRestart.RemoveListener(RestartModule);
                pauseMenu.onExit.RemoveListener(ExitToHub);
                pauseMenu.onFinish.RemoveListener(FinishAttempt);
            }
            if (debriefPanel != null)
            {
                debriefPanel.onRetry.RemoveListener(Retry);
                debriefPanel.onBackToHub.RemoveListener(ExitToHub);
            }
            subscribed = false;
        }

        protected virtual void Start()
        {
            if (Mission == null)
                return;
            if (string.IsNullOrEmpty(DataDirectory))
                DataDirectory = Application.persistentDataPath;
            started = true;
            EnterBriefing();
        }

        protected virtual void Update()
        {
            if (!started || attemptPaused || leaving)
                return;
            var delta = (double)Time.unscaledDeltaTime;
            if (Phase == ModulePhase.Practice || Phase == ModulePhase.Qualification)
                attemptSeconds += delta;

            switch (Phase)
            {
                case ModulePhase.Briefing:
                    phaseTimer -= delta;
                    if (phaseTimer <= 0.0)
                        EnterDemonstration();
                    break;
                case ModulePhase.Demonstration:
                    phaseTimer -= delta;
                    if (phaseTimer <= 0.0)
                        AdvanceDemonstration();
                    break;
                case ModulePhase.Practice:
                    if (practiceComplete)
                    {
                        phaseTimer -= delta;
                        if (phaseTimer <= 0.0)
                            EnterQualification();
                    }
                    break;
            }
        }

        /// <summary>Pause menu RESTART: back to Briefing (FR-APP-03).</summary>
        public void RestartModule()
        {
            if (!started || leaving)
                return;
            machine.Reset();
            EnterBriefing();
        }

        /// <summary>Debrief RETRY: a fresh Practice attempt.</summary>
        public void Retry()
        {
            if (!started || leaving || Phase != ModulePhase.Debrief)
                return;
            machine.TransitionTo(ModulePhase.Practice);
            EnterPractice();
        }

        /// <summary>Pause menu FINISH (Qualification only): end the attempt and score what was done.</summary>
        public void FinishAttempt()
        {
            if (started && !leaving && Phase == ModulePhase.Qualification)
                CompleteQualification();
        }

        public void ExitToHub()
        {
            if (leaving)
                return;
            leaving = true;
            try
            {
                LoadScene(HubSceneName);
            }
            catch (Exception exception)
            {
                leaving = false;
                Debug.LogException(exception, this);
            }
        }

        private void HandlePause() => attemptPaused = true;
        private void HandleResume() => attemptPaused = false;

        private void HandleReport(InteractionReport report)
        {
            if (!started || leaving || reporting || runner == null)
                return;
            if (Phase != ModulePhase.Practice && Phase != ModulePhase.Qualification)
            {
                TrackPauseReport(report);
                return;
            }
            if (Phase == ModulePhase.Practice && practiceComplete)
            {
                TrackPauseReport(report);
                return;
            }

            var appEvent = report.EventName == EventNames.Paused || report.EventName == EventNames.Resumed ||
                           report.EventName == EventNames.Recentered;
            // Ordinary interaction is suppressed while paused; app-level events are still recorded.
            if (attemptPaused && !appEvent)
                return;

            StepFeedback feedback;
            reporting = true;
            try
            {
                feedback = runner.Report(report.EventName, report.TargetId, report.Input, AttemptTimestampMs());
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"[LevelController] Rejected interaction {report}: {exception.Message}", this);
                return;
            }
            finally
            {
                reporting = false;
            }

            TrackPauseReport(report);
            var recorded = runner.Events[runner.Events.Count - 1];
            EventRecorded?.Invoke(recorded, feedback);
            if (this == null || runner == null)
                return;

            if (Phase == ModulePhase.Practice)
                ShowPracticeFeedback(feedback);
            else if (feedback.Outcome == StepOutcome.Correct)
            {
                if (runner.IsComplete)
                    CompleteQualification();
                else
                    ShowInstruction(runner.CurrentStep.instruction);
            }
        }

        private void TrackPauseReport(InteractionReport report)
        {
            // The bus report precedes PauseMenu.onPause / onResume, so the clock is updated after the
            // event is stamped: paused time is excluded and timestamps never decrease.
            if (report.EventName == EventNames.Paused)
                attemptPaused = true;
            else if (report.EventName == EventNames.Resumed)
                attemptPaused = false;
        }

        private void ShowPracticeFeedback(StepFeedback feedback)
        {
            switch (feedback.Outcome)
            {
                case StepOutcome.Incorrect:
                case StepOutcome.OutOfOrder:
                    if (instructionPanel != null && !string.IsNullOrEmpty(feedback.Hint))
                        instructionPanel.ShowHint(feedback.Hint);
                    break;
                case StepOutcome.Correct:
                    if (instructionPanel != null)
                        instructionPanel.ClearHint();
                    if (runner.IsComplete)
                    {
                        practiceComplete = true;
                        phaseTimer = PracticeToQualificationSeconds;
                        ShowInstruction("Practice complete. Qualification starts in 3 seconds; no hints are shown.");
                    }
                    else
                        ShowInstruction(runner.CurrentStep.instruction);
                    break;
            }
        }

        private void EnterBriefing()
        {
            runner = null;
            practiceComplete = false;
            attemptPaused = false;
            LastResult = null;
            ResetAttemptState(ModulePhase.Briefing);
            SetDebriefVisible(false);
            SetPhase(ModulePhase.Briefing);
            if (instructionPanel != null)
                instructionPanel.ClearHint();
            ShowInstruction(Mission.title + "\n" + Mission.steps[0].instruction);
            phaseTimer = Math.Max(0.0, briefingSeconds);
        }

        private void EnterDemonstration()
        {
            if (demoSecondsPerStep <= 0f)
            {
                machine.TransitionTo(ModulePhase.Practice);
                EnterPractice();
                return;
            }
            machine.TransitionTo(ModulePhase.Demonstration);
            SetPhase(ModulePhase.Demonstration);
            demoIndex = -1;
            AdvanceDemonstration();
        }

        private void AdvanceDemonstration()
        {
            demoIndex++;
            if (demoIndex >= Mission.steps.Count)
            {
                machine.TransitionTo(ModulePhase.Practice);
                EnterPractice();
                return;
            }
            var step = Mission.steps[demoIndex];
            ShowInstruction($"Step {demoIndex + 1} of {Mission.steps.Count}: {step.instruction}");
            phaseTimer = demoSecondsPerStep;
        }

        private void EnterPractice()
        {
            BeginAttempt(ModulePhase.Practice);
            ShowInstruction(runner.CurrentStep.instruction);
        }

        private void EnterQualification()
        {
            machine.TransitionTo(ModulePhase.Qualification);
            BeginAttempt(ModulePhase.Qualification);
            ShowInstruction(runner.CurrentStep.instruction);
        }

        private void BeginAttempt(ModulePhase phase)
        {
            LastResult = null;
            practiceComplete = false;
            attemptPaused = false;
            attemptSeconds = 0.0;
            SetDebriefVisible(false);
            ResetAttemptState(phase);
            runner = new MissionRunner(Mission, Guid.NewGuid().ToString());
            SetPhase(phase);
            if (instructionPanel != null)
                instructionPanel.ClearHint();
        }

        private void CompleteQualification()
        {
            if (Phase != ModulePhase.Qualification || runner == null)
                return;
            SessionResult result;
            try
            {
                result = scorer.Score(Mission, runner.SessionId, runner.Events);
            }
            catch (ArgumentException exception)
            {
                Debug.LogException(exception, this);
                return;
            }

            var trace = new List<SessionEvent>(runner.Events);
            machine.TransitionTo(ModulePhase.Debrief);
            attemptPaused = false;
            SetPhase(ModulePhase.Debrief);
            LastResult = result;
            if (instructionPanel != null)
                instructionPanel.ClearHint();
            ShowInstruction(result.passed ? "Level complete. Review your debrief." : "Review your debrief, then select RETRY.");

            QualificationScored?.Invoke(result);
            if (this == null || Phase != ModulePhase.Debrief)
                return;
            SetDebriefVisible(true);
            if (debriefPanel != null)
                debriefPanel.Show(result, Mission);
            WriteSessionLog(result.sessionId, trace);
            RequestCoach(result, trace);
            SaveProgress(result);
        }

        private void RequestCoach(SessionResult result, List<SessionEvent> trace)
        {
            ICoachClient client;
            try
            {
                client = coachProvider != null ? coachProvider.Client : null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return;
            }
            if (client == null || debriefPanel == null)
                return;

            var request = new CoachRequest { sessionId = result.sessionId, missionId = result.missionId, result = result, events = trace };
            try
            {
                client.RequestDebrief(request, Mission, response =>
                {
                    // Advisory text only: it never changes the result or saved progress (FR-AI-04).
                    if (this == null || response == null || Phase != ModulePhase.Debrief ||
                        LastResult == null || LastResult.sessionId != result.sessionId)
                        return;
                    var text = new StringBuilder(response.summary ?? string.Empty);
                    if (response.hints != null)
                        foreach (var hint in response.hints)
                            if (!string.IsNullOrWhiteSpace(hint))
                                text.Append(text.Length > 0 ? "\n• " : "• ").Append(hint);
                    var isAi = response.modelVersion == null ||
                               !response.modelVersion.StartsWith("offline", StringComparison.Ordinal);
                    debriefPanel.ShowCoachText(text.ToString(), isAi);
                });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void SaveProgress(SessionResult result)
        {
            try
            {
                var store = new FileProgressStore(DataDirectory, state => JsonUtility.ToJson(state, true),
                    JsonUtility.FromJson<ProgressState>);
                var progress = store.Load();
                if (store.LastLoadWarning != null)
                    Debug.LogWarning("[LevelController] " + store.LastLoadWarning, this);
                ProgressRules.RecordAttempt(progress, Mission.level, Mission.missionId, result, AvailableLevels());
                store.Save(progress);
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException("[LevelController] Progress could not be saved.", exception), this);
            }
        }

        private List<int> AvailableLevels()
        {
            var levels = new List<int>();
            foreach (var entry in LevelCatalog.Levels)
            {
                bool available;
                try
                {
                    available = SceneAvailable(entry.SceneName);
                }
                catch (Exception)
                {
                    available = false;
                }
                if (available)
                    levels.Add(entry.Level);
            }
            return levels;
        }

        private void WriteSessionLog(string sessionId, List<SessionEvent> trace)
        {
            try
            {
                var directory = Path.Combine(DataDirectory, "SessionLogs");
                Directory.CreateDirectory(directory);
                var json = new StringBuilder("[\n");
                for (var i = 0; i < trace.Count; i++)
                    json.Append("  ").Append(JsonUtility.ToJson(trace[i])).Append(i + 1 < trace.Count ? ",\n" : "\n");
                json.Append("]\n");
                File.WriteAllText(Path.Combine(directory, sessionId + ".json"), json.ToString(), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[LevelController] Session log not written: {exception.Message}", this);
            }
        }

        private long AttemptTimestampMs() => (long)Math.Floor(attemptSeconds * 1000.0);

        private void SetPhase(ModulePhase phase)
        {
            Phase = phase;
            if (pauseMenu != null)
                pauseMenu.SetPhase(phase);
            if (phaseBanner != null)
                phaseBanner.Show(phase);
            PhaseChanged?.Invoke(phase);
        }

        private void ShowInstruction(string text)
        {
            if (instructionPanel != null)
                instructionPanel.ShowInstruction(text);
        }

        private void SetDebriefVisible(bool visible)
        {
            if (debriefPanel != null && debriefPanel.gameObject.activeSelf != visible)
                debriefPanel.gameObject.SetActive(visible);
        }

        /// <summary>Fresh attempt: interactable latches, cooldowns, tools and the pause overlay reset silently.</summary>
        private void ResetAttemptState(ModulePhase phase)
        {
            if (pauseMenu != null)
                pauseMenu.ResetForAttempt(phase);
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var control in root.GetComponentsInChildren<ControlTarget>(true))
                    control.ResetForAttempt();
                foreach (var tool in root.GetComponentsInChildren<GrabbableTool>(true))
                    tool.ResetForAttempt();
                foreach (var locatable in root.GetComponentsInChildren<LocatableTarget>(true))
                    locatable.ResetForAttempt();
            }
        }
    }
}

using System;
using System.Collections;
using System.IO;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Integrations.Coach;
using Astra.UI;
using Astra.XR.Interactables;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Astra.App
{
    /// <summary>
    /// One per level scene. Loads the mission, runs the phases, forwards InteractionEvents to the
    /// mission runner, scores Qualification, shows the debrief, calls the coach (CONTRACT.md §8).
    /// OWNER: Integration Lead. Level scripts may subscribe to the events but never drive the runner.
    /// </summary>
    public class LevelController : MonoBehaviour
    {
        [Header("Mission")]
        [SerializeField] private TextAsset missionJson;

        [Header("UI (XR & Interaction provides; all optional)")]
        [SerializeField] private InstructionPanel instructionPanel;
        [SerializeField] private PhaseBanner phaseBanner;
        [SerializeField] private DebriefPanel debriefPanel;

        [Header("Coach (Content & AI Coach provides; optional)")]
        [SerializeField] private CoachProvider coachProvider;

        [Header("Timing (CONTRACT.md §8)")]
        [SerializeField] private float briefingSeconds = 6f;
        [SerializeField] private float demoSecondsPerStep = 4f;
        [SerializeField] private float practiceToQualificationSeconds = 3f;

        [Header("Runner")]
        [Tooltip("Use the stand-in runner/scorer even if Core's real classes exist.")]
        [SerializeField] private bool forceMockRunner;

        // ---- contract surface (§8) ----
        public event Action<ModulePhase> PhaseChanged;
        public event Action<SessionEvent, StepFeedback> EventRecorded;
        public event Action<SessionResult> QualificationScored;
        public MissionDefinition Mission { get; private set; }
        public ModulePhase Phase => _fsm.Current;

        // ---- additive, read-only helpers for test tools and level scripts ----
        public event Action<MissionStep> DemonstrationStep;
        public IMissionRunner Runner { get; private set; }
        public SessionResult LastResult { get; private set; }
        public bool IsPaused { get; private set; }
        public bool UsingMockRunner { get; private set; }
        public long AttemptTimeMs => _attemptActive ? NowMs() : 0;

        private readonly ModulePhaseMachine _fsm = new ModulePhaseMachine();
        private IMissionScorer _scorer;
        private Coroutine _phaseRoutine;
        private double _attemptStart, _pausedAt, _pausedTotal;
        private bool _attemptActive;

        protected virtual void Awake()
        {
            if (missionJson == null)
            {
                Debug.LogError($"[LevelController] '{name}': missionJson is not assigned. The level stays paused.", this);
                IsPaused = true;
                enabled = false;
                return;
            }
            Mission = JsonUtility.FromJson<MissionDefinition>(missionJson.text);
            _scorer = CreateScorer();
            if (debriefPanel != null)
            {
                debriefPanel.onRetry.AddListener(Retry);          // RETRY button -> Practice
                debriefPanel.onBackToHub.AddListener(BackToHub);  // HUB button -> Hub scene
            }
        }

        private void OnEnable() => InteractionEvents.Reported += OnInteraction;
        private void OnDisable() => InteractionEvents.Reported -= OnInteraction;
        private void Start() { if (Mission != null) EnterBriefing(); }

        // ---------------- public commands (wired to the pause menu / debrief / test HUD) ----------------
        public void Skip()
        {
            if (Phase == ModulePhase.Briefing) Enter(ModulePhase.Demonstration);
            else if (Phase == ModulePhase.Demonstration) Enter(ModulePhase.Practice);
        }
        public void SkipToPractice() { if (Phase == ModulePhase.Briefing || Phase == ModulePhase.Demonstration) Enter(ModulePhase.Practice); }
        public void Finish() { if (Phase == ModulePhase.Qualification) Enter(ModulePhase.Debrief); }
        public void Retry() { if (Phase == ModulePhase.Debrief) Enter(ModulePhase.Practice); }
        public void Restart() => EnterBriefing();
        public void Recenter() => InteractionEvents.Raise(EventNames.Recentered, SystemTargets.System, InputModality.Simulator);

        public void Pause()
        {
            if (IsPaused) return;
            if (_attemptActive) InteractionEvents.Raise(EventNames.Paused, SystemTargets.System, InputModality.Simulator);
            IsPaused = true; _pausedAt = Clock();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            _pausedTotal += Clock() - _pausedAt; IsPaused = false;
            if (_attemptActive) InteractionEvents.Raise(EventNames.Resumed, SystemTargets.System, InputModality.Simulator);
        }

        public void BackToHub()
        {
            if (Application.CanStreamedLevelBeLoaded("Hub")) SceneManager.LoadScene("Hub");
            else Debug.Log("[LevelController] Hub scene is not in Build Settings yet; staying in the level.");
        }

        // ---------------- phases ----------------
        private void EnterBriefing()
        {
            _fsm.Reset();
            _attemptActive = false; Runner = null; IsPaused = false;
            Announce(ModulePhase.Briefing);
            Run(BriefingRoutine());
        }

        private void Enter(ModulePhase next)
        {
            if (!_fsm.CanTransitionTo(next)) { Debug.LogWarning($"[LevelController] ignored illegal transition {Phase} -> {next}"); return; }
            _fsm.TransitionTo(next);
            Announce(next);
            switch (next)
            {
                case ModulePhase.Demonstration: _attemptActive = false; Run(DemoRoutine()); break;
                case ModulePhase.Practice:
                case ModulePhase.Qualification: StartAttempt(); break;
                case ModulePhase.Debrief: _attemptActive = false; Debrief(); break;
            }
        }

        private void Announce(ModulePhase phase)
        {
            if (_phaseRoutine != null) { StopCoroutine(_phaseRoutine); _phaseRoutine = null; }
            if (phaseBanner != null) phaseBanner.Show(phase);
            if (debriefPanel != null) debriefPanel.gameObject.SetActive(phase == ModulePhase.Debrief);   // only during Debrief
            ClearHint();
            PhaseChanged?.Invoke(phase);
        }

        private IEnumerator BriefingRoutine()
        {
            ShowInstruction(Mission.title);
            yield return WaitUnpaused(briefingSeconds);
            Enter(ModulePhase.Demonstration);
        }

        private IEnumerator DemoRoutine()
        {
            foreach (var step in Sorted())
            {
                ShowInstruction(step.instruction);
                DemonstrationStep?.Invoke(step);
                yield return WaitUnpaused(demoSecondsPerStep);
            }
            DemonstrationStep?.Invoke(null);
            Enter(ModulePhase.Practice);
        }

        private void StartAttempt()
        {
            Runner = CreateRunner(Guid.NewGuid().ToString("N"));
            _attemptStart = Clock(); _pausedTotal = 0; IsPaused = false; _attemptActive = true;
            ShowInstruction(Runner.CurrentStep?.instruction ?? "");
        }

        private void OnInteraction(InteractionReport report)
        {
            if (!_attemptActive || Runner == null) return;
            bool lifecycle = report.EventName == EventNames.Paused || report.EventName == EventNames.Resumed;
            if (IsPaused && !lifecycle) return;

            StepFeedback fb;
            try { fb = Runner.Report(report.EventName, report.TargetId, report.Input, NowMs()); }
            catch (ArgumentException ex) { Debug.LogWarning($"[LevelController] rejected {report}: {ex.Message}"); return; }

            var events = Runner.Events;
            EventRecorded?.Invoke(events[events.Count - 1], fb);

            if (Phase == ModulePhase.Practice)
            {
                if (fb.Outcome == StepOutcome.Incorrect || fb.Outcome == StepOutcome.OutOfOrder) { if (fb.Hint != null) ShowHint(fb.Hint); }
                else if (fb.Outcome == StepOutcome.Correct) { ClearHint(); ShowInstruction(Runner.CurrentStep?.instruction ?? ""); }
                if (Runner.IsComplete) Run(PracticeCompleteRoutine());
            }
            else if (Phase == ModulePhase.Qualification)
            {
                if (fb.Outcome == StepOutcome.Correct) ShowInstruction(Runner.CurrentStep?.instruction ?? "");
                if (Runner.IsComplete) Enter(ModulePhase.Debrief);
            }
        }

        private IEnumerator PracticeCompleteRoutine()
        {
            _attemptActive = false;
            ShowInstruction("Practice complete. Qualification is next: it is scored and gives no hints.");
            yield return WaitUnpaused(practiceToQualificationSeconds);
            Enter(ModulePhase.Qualification);
        }

        private void Debrief()
        {
            var trace = Runner != null ? Runner.Events : (System.Collections.Generic.IReadOnlyList<SessionEvent>)Array.Empty<SessionEvent>();
            string sessionId = Runner?.SessionId ?? Guid.NewGuid().ToString("N");
            try { LastResult = _scorer.Score(Mission, sessionId, trace); }
            catch (ArgumentException ex) { Debug.LogError($"[LevelController] scoring failed: {ex.Message}"); return; }

            QualificationScored?.Invoke(LastResult);
            if (debriefPanel != null) debriefPanel.Show(LastResult, Mission);
            WriteSessionLog(LastResult, trace);

            var client = coachProvider != null ? coachProvider.Client : null;
            if (client == null || debriefPanel == null) return;
            var request = new CoachRequest { sessionId = sessionId, missionId = Mission.missionId, result = LastResult };
            request.events.AddRange(trace);
            client.RequestDebrief(request, Mission, r =>
            {
                if (r == null || string.IsNullOrEmpty(r.summary)) return;
                string text = r.summary + (r.hints != null && r.hints.Count > 0 ? "\n• " + string.Join("\n• ", r.hints) : "");
                bool isAi = r.modelVersion != null && !r.modelVersion.StartsWith("offline");
                debriefPanel.ShowCoachText(text, isAi);
            });
        }

        // UnityEngine.Object fields: use explicit != null (Unity's fake-null objects defeat ?.)
        private void ShowInstruction(string text) { if (instructionPanel != null) instructionPanel.ShowInstruction(text); }
        private void ShowHint(string text) { if (instructionPanel != null) instructionPanel.ShowHint(text); }
        private void ClearHint() { if (instructionPanel != null) instructionPanel.ClearHint(); }

        // ---------------- helpers ----------------
        private System.Collections.Generic.List<MissionStep> Sorted()
        {
            var list = new System.Collections.Generic.List<MissionStep>(Mission.steps);
            list.Sort((a, b) => a.order.CompareTo(b.order));
            return list;
        }

        private void Run(IEnumerator routine)
        {
            if (_phaseRoutine != null) StopCoroutine(_phaseRoutine);
            _phaseRoutine = StartCoroutine(routine);
        }

        private IEnumerator WaitUnpaused(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds) { if (!IsPaused) elapsed += Time.unscaledDeltaTime; yield return null; }
        }

        private static double Clock() => Time.realtimeSinceStartupAsDouble * 1000.0;
        private long NowMs()
        {
            double now = IsPaused ? _pausedAt : Clock();
            return Math.Max(0L, (long)Math.Round(now - _attemptStart - _pausedTotal));
        }

        private IMissionRunner CreateRunner(string sessionId)
        {
            var real = forceMockRunner ? null : Type.GetType("Astra.Core.Mission.MissionRunner, ASTRA.Core");
            UsingMockRunner = real == null;
            return real != null
                ? (IMissionRunner)Activator.CreateInstance(real, Mission, sessionId)
                : new MockMissionRunner(Mission, sessionId);
        }

        private IMissionScorer CreateScorer()
        {
            var real = forceMockRunner ? null : Type.GetType("Astra.Core.Assessment.MissionScorer, ASTRA.Core");
            return real != null ? (IMissionScorer)Activator.CreateInstance(real) : new MockMissionScorer();
        }

        private void WriteSessionLog(SessionResult result, System.Collections.Generic.IReadOnlyList<SessionEvent> trace)
        {
            try
            {
                var log = new CoachRequest { sessionId = result.sessionId, missionId = result.missionId, result = result };
                log.events.AddRange(trace);
                string dir = Path.Combine(Application.persistentDataPath, "SessionLogs");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, result.sessionId + ".json"), JsonUtility.ToJson(log, true));
            }
            catch (Exception ex) { Debug.LogWarning($"[LevelController] could not write session log: {ex.Message}"); }
        }
    }
}

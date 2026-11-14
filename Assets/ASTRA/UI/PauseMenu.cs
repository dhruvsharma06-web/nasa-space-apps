using System;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.XR.Input;
using Astra.XR.Interactables;
using UnityEngine;
using UnityEngine.Events;

namespace Astra.UI
{
    /// <summary>Pause UI and requests; attempt clocks, phase progression and scene loads remain in App.</summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private GameObject finishButton;
        [SerializeField] private InputModalityTag defaultInteractor;
        [SerializeField] private MonoBehaviour recenterHandler;
        [SerializeField] private UnityEvent pause = new UnityEvent();
        [SerializeField] private UnityEvent resume = new UnityEvent();
        [SerializeField] private UnityEvent recenter = new UnityEvent();
        [SerializeField] private UnityEvent restart = new UnityEvent();
        [SerializeField] private UnityEvent exit = new UnityEvent();
        [SerializeField] private UnityEvent finish = new UnityEvent();

        private ModulePhase phase = ModulePhase.Briefing;
        private bool processing;
        private bool resetting;
        private bool finishRequested;
        private bool applyingVisibility;
        private bool visibilityDirty;
        private uint revision;

        public UnityEvent onPause => pause;
        public UnityEvent onResume => resume;
        public UnityEvent onRecenter => recenter;
        public UnityEvent onRestart => restart;
        public UnityEvent onExit => exit;
        public UnityEvent onFinish => finish;
        public bool IsPaused { get; private set; }
        public bool CanRecenter => recenterHandler != null && recenterHandler.isActiveAndEnabled &&
                                   recenterHandler is IRecenterHandler;

        public void Configure(GameObject root, GameObject finishControl, InputModalityTag input,
            MonoBehaviour recenterService = null)
        {
            if (root == null || finishControl == null)
                throw new ArgumentException("menuRoot and finishButton references are required.");
            if (transform.IsChildOf(root.transform))
                throw new ArgumentException("PauseMenu must remain outside the root it hides.");
            if (finishControl == root || !finishControl.transform.IsChildOf(root.transform))
                throw new ArgumentException("finishButton must be a child of menuRoot.");
            if (recenterService != null && !(recenterService is IRecenterHandler))
                throw new ArgumentException("Recenter service must implement IRecenterHandler.", nameof(recenterService));

            menuRoot = root;
            finishButton = finishControl;
            defaultInteractor = input;
            recenterHandler = recenterService;
            revision++;
        }

        // Parameterless methods serve UnityEvent bindings. A tracked pointer that invokes the binding
        // publishes itself through UiActionActor, so the report carries the modality that actually acted;
        // otherwise (desktop EventSystem click) the explicitly configured default interactor is used.
        public void Pause() => TryPause(ActingInteractor);
        public void Resume() => TryResume(ActingInteractor);
        public void Toggle() => TryToggle(ActingInteractor);
        public void Recenter() => TryRecenter(ActingInteractor);

        private Component ActingInteractor => UiActionActor.Current != null ? UiActionActor.Current : defaultInteractor;

        public bool TryPause(Component interactor) => TrySetPaused(true, interactor);
        public bool TryResume(Component interactor) => TrySetPaused(false, interactor);
        public bool TryToggle(Component interactor) => TrySetPaused(!IsPaused, interactor);

        public bool TryRecenter(Component interactor)
        {
            if (!isActiveAndEnabled || processing || resetting ||
                !InputModalityTag.TryResolve(interactor, out _) || !CanRecenter)
                return false;

            processing = true;
            var operationRevision = revision;
            var completed = false;
            try
            {
                // The rig is the only publisher. onRecenter is a completion notification,
                // and must not be wired to request another recenter or publish another report.
                completed = ((IRecenterHandler)recenterHandler).TryRecenter(interactor);
                if (completed && this != null && isActiveAndEnabled && revision == operationRevision)
                    recenter.Invoke();
                return completed;
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    completed ? "[PauseMenu] Recenter completed, but its notification failed." :
                    "[PauseMenu] Recenter failed; pause controls remain available.", exception), this);
                return completed;
            }
            finally
            {
                processing = false;
            }
        }

        public void SetPhase(ModulePhase value)
        {
            if (!Enum.IsDefined(typeof(ModulePhase), value))
            {
                Debug.LogError($"[PauseMenu] {name}: invalid phase '{value}'.", this);
                return;
            }
            if (!ValidateReferences())
                return;

            if (phase != value)
            {
                revision++;
                finishRequested = false;
            }
            phase = value;
            if (value != ModulePhase.Practice && value != ModulePhase.Qualification)
                IsPaused = false;
            RefreshVisibility();
        }

        public void ResetForAttempt(ModulePhase value = ModulePhase.Briefing)
        {
            if (resetting || !ValidateReferences())
                return;
            if (!Enum.IsDefined(typeof(ModulePhase), value))
            {
                Debug.LogError($"[PauseMenu] {name}: invalid phase '{value}'.", this);
                return;
            }
            resetting = true;
            try
            {
                revision++;
                IsPaused = false;
                finishRequested = false;
                phase = value;
                RefreshVisibility();
            }
            finally
            {
                resetting = false;
            }
        }

        public void RequestRestart()
        {
            InvokeRequest(restart);
        }

        public void RequestExit()
        {
            InvokeRequest(exit);
        }

        public void RequestFinish()
        {
            if (!isActiveAndEnabled || processing || resetting || finishRequested ||
                phase != ModulePhase.Qualification || !ValidateReferences())
                return;

            processing = true;
            var operationRevision = revision;
            try
            {
                finishRequested = true;
                RefreshVisibility();
                if (this != null && isActiveAndEnabled && revision == operationRevision &&
                    phase == ModulePhase.Qualification && finishRequested)
                    finish.Invoke();
            }
            finally
            {
                processing = false;
            }
        }

        private bool TrySetPaused(bool paused, Component interactor)
        {
            if (!isActiveAndEnabled || processing || resetting || IsPaused == paused ||
                !ValidateReferences() || !InputModalityTag.TryResolve(interactor, out var input))
                return false;

            processing = true;
            var operationRevision = revision;
            try
            {
                IsPaused = paused;
                RefreshVisibility();
                if (this == null || !isActiveAndEnabled || operationRevision != revision || IsPaused != paused)
                    return false;
                InteractionEvents.Raise(paused ? EventNames.Paused : EventNames.Resumed, SystemTargets.System, input);
                if (this != null && isActiveAndEnabled && operationRevision == revision)
                {
                    if (paused)
                        pause.Invoke();
                    else
                        resume.Invoke();
                }
                return true;
            }
            finally
            {
                processing = false;
            }
        }

        private void InvokeRequest(UnityEvent request)
        {
            if (!isActiveAndEnabled || processing || resetting)
                return;
            processing = true;
            try
            {
                request.Invoke();
            }
            finally
            {
                processing = false;
            }
        }

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }
            ResetForAttempt();
        }

        private void OnDisable()
        {
            revision++;
            IsPaused = false;
            RefreshVisibility();
        }

        private void LateUpdate()
        {
            if (visibilityDirty)
                RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            visibilityDirty = true;
            if (applyingVisibility || menuRoot == null || finishButton == null)
                return;

            applyingVisibility = true;
            try
            {
                // Activation callbacks may reset the attempt. Queue their visual changes until
                // the current SetActive has returned; Unity forbids nested hierarchy activation.
                for (var pass = 0; visibilityDirty && pass < 4; pass++)
                {
                    visibilityDirty = false;
                    if (this == null || menuRoot == null || finishButton == null)
                        return;
                    var showMenu = IsPaused && isActiveAndEnabled;
                    if (!showMenu && menuRoot.activeSelf)
                        menuRoot.SetActive(false);

                    var showFinish = phase == ModulePhase.Qualification && !finishRequested;
                    if (finishButton.activeSelf != showFinish)
                        finishButton.SetActive(showFinish);

                    showMenu = IsPaused && isActiveAndEnabled;
                    if (menuRoot.activeSelf != showMenu)
                        menuRoot.SetActive(showMenu);
                }
            }
            finally
            {
                applyingVisibility = false;
            }
        }

        private bool ValidateReferences()
        {
            if (menuRoot == null)
                Debug.LogError($"[PauseMenu] {name}: missing menuRoot reference.", this);
            if (finishButton == null)
                Debug.LogError($"[PauseMenu] {name}: missing finishButton reference.", this);
            if (menuRoot == null || finishButton == null)
                return false;
            if (transform.IsChildOf(menuRoot.transform) || finishButton == menuRoot ||
                !finishButton.transform.IsChildOf(menuRoot.transform))
            {
                Debug.LogError($"[PauseMenu] {name}: menuRoot must exclude its handler and contain finishButton.", this);
                return false;
            }
            return true;
        }
    }
}

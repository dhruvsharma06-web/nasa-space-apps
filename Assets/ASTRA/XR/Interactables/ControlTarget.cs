using System;
using Astra.Core.Assessment;
using Astra.XR.Input;
using UnityEngine;
using UnityEngine.Events;

namespace Astra.XR.Interactables
{
    /// <summary>Shared control validation, feedback and one-report activation boundary.</summary>
    public abstract class ControlTarget : MonoBehaviour
    {
        private const double ActivationCooldownSeconds = 0.3;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color = Shader.PropertyToID("_Color");

        [SerializeField] private string targetId;
        [SerializeField] private string eventName = EventNames.ControlActivated;
        [SerializeField] private Renderer feedbackRenderer;
        [SerializeField] private UnityEngine.Color idleColor = UnityEngine.Color.white;
        [SerializeField] private UnityEngine.Color hoverColor = new UnityEngine.Color(0.2f, 0.8f, 1f);
        [SerializeField] private UnityEngine.Color pressedColor = new UnityEngine.Color(0.3f, 1f, 0.5f);
        [SerializeField] private UnityEvent<bool> onHoverChanged = new UnityEvent<bool>();
        [SerializeField] private UnityEvent<bool> onPressedChanged = new UnityEvent<bool>();

        private MaterialPropertyBlock feedbackProperties;
        private double nextActivationTime = double.NegativeInfinity;
        private double pressedUntil;
        private bool activationInProgress;
        private bool resetInProgress;
        private uint stateRevision;

        public string TargetId => targetId;
        public string EventName => eventName;
        public bool IsHovered { get; private set; }
        public bool IsPressed { get; private set; }
        protected bool ActivationInProgress => activationInProgress || resetInProgress;

        public void Configure(string id, string reportedEvent = EventNames.ControlActivated)
        {
            if (!InteractionValidation.IsTargetId(id))
                throw new ArgumentException("targetId must contain 1–64 lowercase snake_case characters.", nameof(id));
            if (!IsControlEvent(reportedEvent))
                throw new ArgumentException("Control event is not permitted by CONTRACT.md §3.", nameof(reportedEvent));

            targetId = id;
            eventName = reportedEvent;
            stateRevision++;
        }

        public bool TryActivate(Component interactor)
        {
            if (!isActiveAndEnabled || ActivationInProgress ||
                !InteractionValidation.IsTargetId(targetId) || !IsControlEvent(eventName) ||
                !CanActivateFrom(interactor) ||
                Time.unscaledTimeAsDouble < nextActivationTime ||
                !InputModalityTag.TryResolve(interactor, out var input))
                return false;

            activationInProgress = true;
            var revision = stateRevision;
            var reportedTarget = targetId;
            var reportedEvent = eventName;
            try
            {
                if (!ApplyActivation())
                    return false;

                nextActivationTime = Time.unscaledTimeAsDouble + ActivationCooldownSeconds;
                pressedUntil = nextActivationTime;
                var pressedChanged = !IsPressed;
                IsPressed = true;
                RefreshFeedback();
                InteractionEvents.Raise(reportedEvent, reportedTarget, input);
                if (this != null && isActiveAndEnabled && stateRevision == revision)
                {
                    if (pressedChanged)
                        onPressedChanged.Invoke(true);
                    if (this != null && isActiveAndEnabled && stateRevision == revision)
                        OnActivationCommitted();
                }
                return true;
            }
            finally
            {
                activationInProgress = false;
            }
        }

        public void SetHovered(bool hovered)
        {
            if (resetInProgress || IsHovered == hovered)
                return;

            IsHovered = hovered;
            RefreshFeedback();
            onHoverChanged.Invoke(hovered);
        }

        public void ResetForAttempt()
        {
            if (resetInProgress)
                return;

            resetInProgress = true;
            try
            {
                var revision = ++stateRevision;
                var hoveredChanged = IsHovered;
                var pressedChanged = IsPressed;
                nextActivationTime = double.NegativeInfinity;
                IsHovered = false;
                IsPressed = false;
                ResetStateForAttempt();
                RefreshFeedback();
                if (hoveredChanged)
                    onHoverChanged.Invoke(false);
                if (this != null && stateRevision == revision && pressedChanged)
                    onPressedChanged.Invoke(false);
                if (this != null && stateRevision == revision)
                    OnAttemptReset();
            }
            finally
            {
                resetInProgress = false;
            }
        }

        public static bool IsControlEvent(string value)
        {
            return value == EventNames.TargetSelected || value == EventNames.ControlActivated ||
                   value == EventNames.ChecklistOpened || value == EventNames.StatusReported ||
                   value == EventNames.CalibrationCompleted;
        }

        // State changes here must not invoke external callbacks. Report first, then notify observers.
        protected abstract bool ApplyActivation();
        protected virtual bool CanActivateFrom(Component interactor) => true;
        protected virtual void OnActivationCommitted() { }
        protected virtual void ResetStateForAttempt() { }
        protected virtual void OnAttemptReset() { }

        protected virtual void Awake()
        {
            if (!InteractionValidation.IsTargetId(targetId) || !IsControlEvent(eventName))
            {
                Debug.LogError($"[{GetType().Name}] {name}: missing/invalid targetId or eventName.", this);
                enabled = false;
                return;
            }

            if (feedbackRenderer != null)
                feedbackProperties = new MaterialPropertyBlock();
            RefreshFeedback();
        }

        protected virtual void OnValidate()
        {
            if (!InteractionValidation.IsTargetId(targetId))
                Debug.LogError($"[{GetType().Name}] {name}: targetId must use lowercase snake_case.", this);
            if (!IsControlEvent(eventName))
                Debug.LogError($"[{GetType().Name}] {name}: eventName is not an allowed control event.", this);
        }

        protected virtual void Update()
        {
            if (IsPressed && Time.unscaledTimeAsDouble >= pressedUntil)
                SetPressed(false);
        }

        protected virtual void OnDisable()
        {
            stateRevision++;
            SetHovered(false);
            SetPressed(false);
        }

        private void SetPressed(bool pressed)
        {
            if (IsPressed == pressed)
                return;

            IsPressed = pressed;
            RefreshFeedback();
            onPressedChanged.Invoke(pressed);
        }

        private void RefreshFeedback()
        {
            if (feedbackRenderer == null || feedbackProperties == null)
                return;

            var color = IsPressed ? pressedColor : IsHovered ? hoverColor : idleColor;
            feedbackRenderer.GetPropertyBlock(feedbackProperties);
            feedbackProperties.SetColor(BaseColor, color);
            feedbackProperties.SetColor(Color, color);
            feedbackRenderer.SetPropertyBlock(feedbackProperties);
        }
    }
}

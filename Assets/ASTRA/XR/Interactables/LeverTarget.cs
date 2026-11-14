using UnityEngine;

namespace Astra.XR.Interactables
{
    /// <summary>Reports one rising threshold crossing, with hysteresis before rearming.</summary>
    public sealed class LeverTarget : ControlTarget
    {
        [SerializeField, Range(0.1f, 1f)] private float activationThreshold = 0.8f;
        [SerializeField, Range(0f, 0.9f)] private float resetThreshold = 0.2f;
        private bool latched;
        private Component pullingInteractor;

        public float Pull { get; private set; }

        public bool SetPull(float normalizedPull, Component interactor)
        {
            if (!isActiveAndEnabled || ActivationInProgress ||
                float.IsNaN(normalizedPull) || float.IsInfinity(normalizedPull) ||
                !Astra.XR.Input.InputModalityTag.TryResolve(interactor, out _))
                return false;

            Pull = Mathf.Clamp01(normalizedPull);
            if (Pull <= resetThreshold)
                latched = false;
            if (Pull < activationThreshold || latched)
                return false;

            pullingInteractor = interactor;
            try
            {
                return TryActivate(interactor);
            }
            finally
            {
                pullingInteractor = null;
            }
        }

        protected override bool CanActivateFrom(Component interactor) => pullingInteractor == interactor;

        protected override bool ApplyActivation()
        {
            if (Pull < activationThreshold || latched)
                return false;
            latched = true;
            return true;
        }

        protected override void ResetStateForAttempt()
        {
            Pull = 0;
            latched = false;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (!HasValidThresholds())
                Debug.LogError($"[LeverTarget] {name}: resetThreshold must be below activationThreshold.", this);
        }

        protected override void Awake()
        {
            base.Awake();
            if (!HasValidThresholds())
            {
                Debug.LogError($"[LeverTarget] {name}: configure finite thresholds in [0, 1], reset below activation.", this);
                enabled = false;
            }
        }

        private bool HasValidThresholds()
        {
            return !float.IsNaN(resetThreshold) && !float.IsInfinity(resetThreshold) &&
                   !float.IsNaN(activationThreshold) && !float.IsInfinity(activationThreshold) &&
                   resetThreshold >= 0f && resetThreshold < activationThreshold && activationThreshold <= 1f;
        }
    }
}

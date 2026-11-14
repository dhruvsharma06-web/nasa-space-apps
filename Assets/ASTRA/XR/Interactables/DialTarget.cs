using UnityEngine;
using UnityEngine.Events;

namespace Astra.XR.Interactables
{
    /// <summary>Snaps an adapter-supplied local angle to detents; reports entry to the trigger detent.</summary>
    public sealed class DialTarget : ControlTarget
    {
        [SerializeField] private float[] detentAngles = { 0f, 90f, 180f };
        [SerializeField] private int triggerDetent = 1;
        [SerializeField] private UnityEvent<int> onDetentChanged = new UnityEvent<int>();
        private bool updatingDetent;
        private bool pendingTrigger;
        private Component pendingInteractor;

        public int CurrentDetent { get; private set; }

        public bool SetAngle(float angle, Component interactor)
        {
            if (!isActiveAndEnabled || ActivationInProgress || updatingDetent ||
                float.IsNaN(angle) || float.IsInfinity(angle) ||
                !HasValidDetents() || !Astra.XR.Input.InputModalityTag.TryResolve(interactor, out _))
                return false;

            var closest = 0;
            var closestDistance = Mathf.Abs(Mathf.DeltaAngle(angle, detentAngles[0]));
            for (var index = 1; index < detentAngles.Length; index++)
            {
                var distance = Mathf.Abs(Mathf.DeltaAngle(angle, detentAngles[index]));
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = index;
                }
            }

            if (closest == CurrentDetent)
            {
                pendingTrigger = false;
                pendingInteractor = null;
                return false;
            }

            updatingDetent = true;
            try
            {
                // Preserve one-shot adapter input during cooldown; Update retries until cancelled.
                if (closest == triggerDetent)
                {
                    pendingTrigger = true;
                    pendingInteractor = interactor;
                    return TryActivate(interactor);
                }

                pendingTrigger = false;
                pendingInteractor = null;
                CurrentDetent = closest;
                onDetentChanged.Invoke(closest);
                return false;
            }
            finally
            {
                updatingDetent = false;
            }
        }

        protected override bool CanActivateFrom(Component interactor) =>
            pendingTrigger && pendingInteractor == interactor;

        protected override bool ApplyActivation()
        {
            if (!pendingTrigger || CurrentDetent == triggerDetent)
                return false;
            CurrentDetent = triggerDetent;
            pendingTrigger = false;
            pendingInteractor = null;
            return true;
        }

        protected override void OnActivationCommitted() => onDetentChanged.Invoke(CurrentDetent);

        protected override void ResetStateForAttempt()
        {
            CurrentDetent = 0;
            pendingTrigger = false;
            pendingInteractor = null;
        }

        protected override void OnAttemptReset() => onDetentChanged.Invoke(CurrentDetent);

        protected override void Update()
        {
            base.Update();
            if (!pendingTrigger)
                return;
            if (!Astra.XR.Input.InputModalityTag.TryResolve(pendingInteractor, out _))
            {
                pendingTrigger = false;
                pendingInteractor = null;
                return;
            }
            TryActivate(pendingInteractor);
        }

        protected override void OnDisable()
        {
            pendingTrigger = false;
            pendingInteractor = null;
            base.OnDisable();
        }

        protected override void Awake()
        {
            base.Awake();
            if (!HasValidDetents())
            {
                Debug.LogError($"[DialTarget] {name}: detentAngles/triggerDetent are invalid.", this);
                enabled = false;
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (!HasValidDetents())
                Debug.LogError($"[DialTarget] {name}: configure finite detent angles and a valid triggerDetent.", this);
        }

        private bool HasValidDetents()
        {
            if (detentAngles == null || detentAngles.Length < 2 ||
                triggerDetent < 0 || triggerDetent >= detentAngles.Length)
                return false;
            for (var index = 0; index < detentAngles.Length; index++)
                if (float.IsNaN(detentAngles[index]) || float.IsInfinity(detentAngles[index]))
                    return false;
            return true;
        }
    }
}

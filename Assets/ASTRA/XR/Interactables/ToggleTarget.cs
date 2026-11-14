using UnityEngine;
using UnityEngine.Events;

namespace Astra.XR.Interactables
{
    public sealed class ToggleTarget : ControlTarget
    {
        [SerializeField] private bool initialState;
        [SerializeField] private UnityEvent<bool> onStateChanged = new UnityEvent<bool>();

        public bool IsOn { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            IsOn = initialState;
        }

        protected override bool ApplyActivation()
        {
            IsOn = !IsOn;
            return true;
        }

        protected override void OnActivationCommitted() => onStateChanged.Invoke(IsOn);

        protected override void ResetStateForAttempt()
        {
            IsOn = initialState;
        }

        protected override void OnAttemptReset() => onStateChanged.Invoke(IsOn);
    }
}

using System;
using Astra.Core.Assessment;
using Astra.XR.Input;
using UnityEngine;

namespace Astra.XR.Interactables
{
    /// <summary>Adapter-maintained ray/gaze dwell. Attempt composition calls ResetForAttempt.</summary>
    public sealed class LocatableTarget : MonoBehaviour
    {
        private const double DwellSeconds = 1.0;
        [SerializeField] private string targetId;
        private Component trackingInteractor;
        private double dwellStarted;

        public bool WasLocated { get; private set; }
        public string TargetId => targetId;

        public void Configure(string id)
        {
            if (!InteractionValidation.IsTargetId(id))
                throw new ArgumentException("targetId must use lowercase snake_case.", nameof(id));
            targetId = id;
        }

        public bool BeginDwell(Component interactor)
        {
            if (!isActiveAndEnabled || WasLocated ||
                !InputModalityTag.TryResolve(interactor, out _))
                return false;
            if (trackingInteractor == interactor)
                return true;

            trackingInteractor = interactor;
            dwellStarted = Time.unscaledTimeAsDouble;
            return true;
        }

        public void EndDwell(Component interactor)
        {
            if (trackingInteractor == interactor)
                trackingInteractor = null;
        }

        public void ResetForAttempt()
        {
            trackingInteractor = null;
            WasLocated = false;
        }

        private void Awake()
        {
            if (!InteractionValidation.IsTargetId(targetId))
            {
                Debug.LogError($"[LocatableTarget] {name}: missing/invalid targetId.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (WasLocated || trackingInteractor == null)
                return;
            if (!InputModalityTag.TryResolve(trackingInteractor, out var input))
            {
                trackingInteractor = null;
                return;
            }
            if (Time.unscaledTimeAsDouble - dwellStarted < DwellSeconds)
                return;

            WasLocated = true;
            trackingInteractor = null;
            InteractionEvents.Raise(EventNames.TargetLocated, targetId, input);
        }

        private void OnDisable() => trackingInteractor = null;

        private void OnValidate()
        {
            if (!InteractionValidation.IsTargetId(targetId))
                Debug.LogError($"[LocatableTarget] {name}: targetId must use lowercase snake_case.", this);
        }
    }
}

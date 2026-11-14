using System;
using Astra.Core.Assessment;
using Astra.XR.Input;
using UnityEngine;

namespace Astra.XR.Interactables
{
    /// <summary>One holder and one release outcome. XR adapters supply the actual acting interactor.</summary>
    [DisallowMultipleComponent]
    public sealed class GrabbableTool : MonoBehaviour
    {
        private const double ReturnDelaySeconds = 3.0;
        [SerializeField] private string targetId;
        [SerializeField] private Transform reachOrigin;
        [SerializeField, Min(0.1f)] private float reachableRadius = 0.7f;

        private Component holder;
        private bool held;
        private string heldInput;
        private Rigidbody physicsBody;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Transform homeParent;
        private double returnAt = double.PositiveInfinity;
        private bool kinematicBeforePlacement;
        private bool transitionInProgress;

        public string TargetId => targetId;
        public bool IsHeld => held;
        public PlacementSocket CurrentSocket { get; private set; }

        public void Configure(string id)
        {
            if (!InteractionValidation.IsTargetId(id))
                throw new ArgumentException("targetId must follow the content style guide's ID rules.", nameof(id));
            if (IsHeld || CurrentSocket != null)
                throw new InvalidOperationException("Reset the tool before changing its targetId.");
            targetId = id;
        }

        public bool BeginGrab(Component interactor)
        {
            if (!isActiveAndEnabled || transitionInProgress || IsHeld ||
                !InteractionValidation.IsTargetId(targetId) ||
                !InputModalityTag.TryResolve(interactor, out var input))
                return false;

            transitionInProgress = true;
            try
            {
                DetachFromSocket();
                holder = interactor;
                held = true;
                heldInput = input;
                returnAt = double.PositiveInfinity;
                InteractionEvents.Raise(EventNames.ObjectGrabbed, targetId, input);
                return true;
            }
            finally
            {
                transitionInProgress = false;
            }
        }

        public bool EndGrab(Component interactor, PlacementSocket candidateSocket = null)
        {
            if (!isActiveAndEnabled || transitionInProgress || !IsHeld || holder != interactor ||
                !InputModalityTag.TryResolve(interactor, out var input))
                return false;

            transitionInProgress = true;
            try
            {
                var placed = candidateSocket != null && candidateSocket.TryAttach(this);
                holder = null;
                held = false;
                heldInput = null;
                if (placed)
                {
                    CurrentSocket = candidateSocket;
                    if (physicsBody != null)
                    {
                        kinematicBeforePlacement = physicsBody.isKinematic;
                        StopPhysics();
                        physicsBody.isKinematic = true;
                    }
                    SnapToSocket();
                    returnAt = double.PositiveInfinity;
                    InteractionEvents.Raise(EventNames.ObjectPlaced, candidateSocket.SocketId, input);
                }
                else
                {
                    ScheduleReturnIfNeeded();
                    InteractionEvents.Raise(EventNames.ObjectReleased, targetId, input);
                }
                return true;
            }
            finally
            {
                transitionInProgress = false;
            }
        }

        /// <summary>Composition calls this between attempts, without producing learner actions.</summary>
        public void ResetForAttempt()
        {
            holder = null;
            held = false;
            heldInput = null;
            DetachFromSocket();
            ReturnHome();
        }

        private void Awake()
        {
            homeParent = transform.parent;
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            physicsBody = GetComponent<Rigidbody>();
            if (!InteractionValidation.IsTargetId(targetId) ||
                float.IsNaN(reachableRadius) || float.IsInfinity(reachableRadius) || reachableRadius < 0.1f)
            {
                Debug.LogError($"[GrabbableTool] {name}: configure targetId and a reachableRadius of at least 0.1 m.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (IsHeld && !InputModalityTag.TryResolve(holder, out _))
                CancelGrab();
            if (IsHeld || CurrentSocket != null)
                return;

            ScheduleReturnIfNeeded();
            if (Time.unscaledTimeAsDouble >= returnAt)
                ReturnHome();
        }

        private void LateUpdate()
        {
            if (CurrentSocket != null)
                SnapToSocket();
        }

        private void OnDisable()
        {
            holder = null;
            held = false;
            heldInput = null;
            DetachFromSocket();
            returnAt = double.PositiveInfinity;
        }

        /// <summary>Tracking/adapter loss terminates a grab once using its last validated input route.</summary>
        public bool CancelGrab()
        {
            if (!held || transitionInProgress)
                return false;
            transitionInProgress = true;
            try
            {
                var input = heldInput;
                holder = null;
                held = false;
                heldInput = null;
                ScheduleReturnIfNeeded();
                InteractionEvents.Raise(EventNames.ObjectReleased, targetId, input);
                return true;
            }
            finally
            {
                transitionInProgress = false;
            }
        }

        private void OnValidate()
        {
            if (!InteractionValidation.IsTargetId(targetId))
                Debug.LogError($"[GrabbableTool] {name}: missing/invalid targetId.", this);
        }

        private void ScheduleReturnIfNeeded()
        {
            var origin = reachOrigin != null ? reachOrigin.position : HomeWorldPosition;
            var outOfReach = (transform.position - origin).sqrMagnitude > reachableRadius * reachableRadius;
            if (!outOfReach)
                returnAt = double.PositiveInfinity;
            else if (double.IsPositiveInfinity(returnAt))
                returnAt = Time.unscaledTimeAsDouble + ReturnDelaySeconds;
        }

        private Vector3 HomeWorldPosition => homeParent != null ? homeParent.TransformPoint(homePosition) : homePosition;

        private void ReturnHome()
        {
            StopPhysics();
            var rotation = homeParent != null ? homeParent.rotation * homeRotation : homeRotation;
            transform.SetPositionAndRotation(HomeWorldPosition, rotation);
            returnAt = double.PositiveInfinity;
        }

        private void StopPhysics()
        {
            if (physicsBody == null || physicsBody.isKinematic)
                return;
            physicsBody.linearVelocity = Vector3.zero;
            physicsBody.angularVelocity = Vector3.zero;
        }

        private void SnapToSocket()
        {
            var snap = CurrentSocket.SnapPoint;
            transform.SetPositionAndRotation(snap.position, snap.rotation);
        }

        private void DetachFromSocket()
        {
            if (CurrentSocket == null)
                return;
            CurrentSocket.Detach(this);
            CurrentSocket = null;
            if (physicsBody != null)
                physicsBody.isKinematic = kinematicBeforePlacement;
        }

        internal void SocketUnavailable(PlacementSocket socket)
        {
            if (CurrentSocket != socket)
                return;
            CurrentSocket = null;
            if (physicsBody != null)
                physicsBody.isKinematic = kinematicBeforePlacement;
            ScheduleReturnIfNeeded();
        }
    }
}

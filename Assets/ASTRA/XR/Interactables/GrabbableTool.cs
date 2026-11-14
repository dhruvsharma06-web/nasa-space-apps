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
        private bool releasePending;
        private uint grabRevision;
        private string heldInput;
        private string releaseInput;
        private Rigidbody physicsBody;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Transform homeParent;
        private double returnAt = double.PositiveInfinity;
        private bool kinematicBeforePlacement;
        private bool transitionInProgress;

        public string TargetId => targetId;
        public bool IsHeld => held && !releasePending;
        public bool HasPendingRelease => held && releasePending;
        public PlacementSocket CurrentSocket { get; private set; }

        /// <summary>A captured physical release, valid only for this tool's current grab.</summary>
        public readonly struct ReleaseToken
        {
            internal readonly GrabbableTool Owner;
            internal readonly Component Actor;
            internal readonly string Input;
            internal readonly uint Revision;

            internal ReleaseToken(GrabbableTool owner, Component actor, string input, uint revision)
            {
                Owner = owner;
                Actor = actor;
                Input = input;
                Revision = revision;
            }
        }

        public void Configure(string id)
        {
            if (!InteractionValidation.IsTargetId(id))
                throw new ArgumentException("targetId must follow the content style guide's ID rules.", nameof(id));
            if (held || CurrentSocket != null)
                throw new InvalidOperationException("Reset the tool before changing its targetId.");
            targetId = id;
        }

        public bool BeginGrab(Component interactor)
        {
            if (!isActiveAndEnabled || transitionInProgress || held ||
                !InteractionValidation.IsTargetId(targetId) ||
                !InputModalityTag.TryResolve(interactor, out var input))
                return false;

            transitionInProgress = true;
            try
            {
                DetachFromSocket();
                holder = interactor;
                held = true;
                releasePending = false;
                releaseInput = null;
                grabRevision++;
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
            return PrepareRelease(interactor, out var token) && CompleteRelease(token, candidateSocket);
        }

        /// <summary>Capture the actor's route now; an adapter can then observe the actual socket transfer.</summary>
        public bool PrepareRelease(Component interactor, out ReleaseToken token)
        {
            token = default;
            if (!isActiveAndEnabled || transitionInProgress || !held || releasePending || holder != interactor ||
                !InputModalityTag.TryResolve(interactor, out var input))
                return false;
            releasePending = true;
            releaseInput = input;
            token = new ReleaseToken(this, interactor, input, grabRevision);
            return true;
        }

        public bool CompleteRelease(ReleaseToken token, PlacementSocket candidateSocket = null)
        {
            if (!isActiveAndEnabled || transitionInProgress || !held || !releasePending ||
                !ReferenceEquals(token.Owner, this) || !ReferenceEquals(token.Actor, holder) ||
                token.Revision != grabRevision || !InputModalityTag.IsKnown(token.Input))
                return false;

            transitionInProgress = true;
            try
            {
                var placed = candidateSocket != null && candidateSocket.TryAttach(this);
                holder = null;
                held = false;
                releasePending = false;
                releaseInput = null;
                grabRevision++;
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
                    InteractionEvents.Raise(EventNames.ObjectPlaced, candidateSocket.SocketId, token.Input);
                }
                else
                {
                    ScheduleReturnIfNeeded();
                    InteractionEvents.Raise(EventNames.ObjectReleased, targetId, token.Input);
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
            releasePending = false;
            releaseInput = null;
            grabRevision++;
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
            if (held && !releasePending && !InputModalityTag.TryResolve(holder, out _))
                CancelGrab();
            if (held || CurrentSocket != null)
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
            releasePending = false;
            releaseInput = null;
            grabRevision++;
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
                var input = releasePending ? releaseInput : heldInput;
                holder = null;
                held = false;
                releasePending = false;
                releaseInput = null;
                grabRevision++;
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

using System;
using UnityEngine;

namespace Astra.XR.Interactables
{
    /// <summary>Accepts one matching tool. The tool publishes the single terminal release outcome.</summary>
    [DisallowMultipleComponent]
    public sealed class PlacementSocket : MonoBehaviour
    {
        [SerializeField] private string socketId;
        [SerializeField] private string acceptsToolId;
        [SerializeField] private Transform snapPoint;

        public string SocketId => socketId;
        public GrabbableTool Occupant { get; private set; }
        public Transform SnapPoint => snapPoint != null ? snapPoint : transform;

        public void Configure(string id, string acceptedToolId)
        {
            if (!InteractionValidation.IsTargetId(id) || !InteractionValidation.IsTargetId(acceptedToolId))
                throw new ArgumentException("Socket and accepted tool IDs must follow the content style guide's ID rules.");
            if (Occupant != null)
                throw new InvalidOperationException("Remove the occupying tool before configuring a socket.");
            socketId = id;
            acceptsToolId = acceptedToolId;
        }

        public bool CanAccept(GrabbableTool tool)
        {
            return isActiveAndEnabled && tool != null && tool.isActiveAndEnabled &&
                   InteractionValidation.IsTargetId(socketId) &&
                   tool.TargetId == acceptsToolId && (Occupant == null || Occupant == tool);
        }

        public bool TryPlace(GrabbableTool tool, Component interactor)
        {
            if (tool == null)
                return false;
            var accepted = CanAccept(tool);
            return tool.EndGrab(interactor, this) && accepted;
        }

        internal bool TryAttach(GrabbableTool tool)
        {
            if (!CanAccept(tool))
                return false;
            Occupant = tool;
            return true;
        }

        internal void Detach(GrabbableTool tool)
        {
            if (Occupant == tool)
                Occupant = null;
        }

        private void Awake()
        {
            if (!InteractionValidation.IsTargetId(socketId) || !InteractionValidation.IsTargetId(acceptsToolId))
            {
                Debug.LogError($"[PlacementSocket] {name}: configure socketId and acceptsToolId.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            var tool = Occupant;
            Occupant = null;
            if (tool != null)
                tool.SocketUnavailable(this);
        }

        private void OnValidate()
        {
            if (!InteractionValidation.IsTargetId(socketId) || !InteractionValidation.IsTargetId(acceptsToolId))
                Debug.LogError($"[PlacementSocket] {name}: socketId and acceptsToolId must follow content ID rules.", this);
        }
    }
}

using Astra.XR.Input;
using Astra.XR.Interactables;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Astra.XR.Rig
{
    /// <summary>
    /// Shared ray interaction used by every pointer route (headset hand aim, desktop mouse).
    /// An input route supplies one <see cref="Frame"/> per update; this class drives the contract
    /// interactables through their adapter entry points with the actor that actually performed each action.
    /// It never scores, timestamps or changes mission state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RayInteractor : MonoBehaviour
    {
        public struct Frame
        {
            public Ray Ray;
            public bool Valid;
            /// <summary>Select/activate started this frame (pinch, trigger or mouse press).</summary>
            public bool SelectDown;
            /// <summary>Grab started this frame (fist/grip).</summary>
            public bool GrabDown;
            /// <summary>Grab is held (fist/grip, or the mouse button for desktop).</summary>
            public bool GrabHeld;
            /// <summary>Desktop: the select press also grabs a tool under the pointer.</summary>
            public bool SelectGrabs;
            /// <summary>Activate world-space UI buttons from this ray (tracked pointers without an EventSystem module).</summary>
            public bool ClickUi;
            public Component SelectActor;
            public Component GrabActor;
            public float HoldDistanceDelta;
        }

        [SerializeField, Min(0.1f)] private float maxDistance = 3f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField, Min(0.01f)] private float socketCaptureRadius = 0.12f;
        [SerializeField, Min(0.05f)] private float minHoldDistance = 0.2f;
        [SerializeField, Min(0.1f)] private float maxHoldDistance = 0.75f;

        private readonly RaycastHit[] worldHits = new RaycastHit[16];
        private readonly Collider[] overlapHits = new Collider[16];
        private ControlTarget hoveredControl;
        private LocatableTarget dwellTarget;
        private Component dwellActor;
        private Selectable hoveredSelectable;
        private GrabbableTool heldTool;
        private Rigidbody heldBody;
        private bool heldBodyWasKinematic;
        private float holdDistance;

        public GrabbableTool HeldTool => heldTool;
        public bool HasHit { get; private set; }
        /// <summary>The ray is on a control, a tool or a world-space UI panel (anything a tap should act on).</summary>
        public bool PointsAtTarget { get; private set; }
        public Vector3 HitPoint { get; private set; }
        public float MaxDistance => maxDistance;

        /// <summary>Processes one pointer update. Returns true when the ray currently points at something.</summary>
        public bool Step(in Frame frame)
        {
            HasHit = false;
            PointsAtTarget = false;
            if (!isActiveAndEnabled || !frame.Valid)
            {
                Cancel();
                return false;
            }

            // An attempt reset or tracking-loss handling may have ended the grab outside this adapter.
            if (heldTool != null && !heldTool.IsHeld)
            {
                RestoreHeldBody();
                ClearHeld();
            }

            var ray = frame.Ray;
            var uiHit = WorldUiRaycast.Raycast(ray, maxDistance, out var uiDistance, out var selectable);
            var worldHit = RaycastWorld(ray, out var hit);
            var pointsAtWorld = worldHit && (!uiHit || hit.distance < uiDistance);

            if (heldTool != null)
            {
                holdDistance = Mathf.Clamp(holdDistance + frame.HoldDistanceDelta, minHoldDistance, maxHoldDistance);
                heldTool.transform.position = ray.GetPoint(holdDistance);
                HasHit = true;
                PointsAtTarget = true;
                HitPoint = heldTool.transform.position;
                UpdateHover(null);
                UpdateDwell(null, null);
                UpdateUiHover(null);
                if (!frame.GrabHeld)
                    ReleaseHeld(ray, frame.GrabActor);
                return true;
            }

            var collider = pointsAtWorld ? hit.collider : null;
            UpdateHover(collider != null ? collider.GetComponentInParent<ControlTarget>() : null);
            UpdateDwell(collider != null ? collider.GetComponentInParent<LocatableTarget>() : null, frame.SelectActor);
            UpdateUiHover(!pointsAtWorld && frame.ClickUi ? selectable : null);

            if (pointsAtWorld)
            {
                HasHit = true;
                HitPoint = hit.point;
            }
            else if (uiHit)
            {
                HasHit = true;
                HitPoint = ray.GetPoint(uiDistance);
            }

            if (!pointsAtWorld)
            {
                PointsAtTarget = uiHit;
                if (frame.ClickUi && frame.SelectDown && selectable != null)
                    ClickUi(selectable, frame.SelectActor);
                return HasHit;
            }

            var tool = collider.GetComponentInParent<GrabbableTool>();
            PointsAtTarget = tool != null || collider.GetComponentInParent<ControlTarget>() != null;
            if (tool != null && (frame.GrabDown || (frame.SelectGrabs && frame.SelectDown)))
            {
                var actor = frame.SelectGrabs && !frame.GrabDown ? frame.SelectActor : frame.GrabActor;
                if (actor != null && tool.BeginGrab(actor))
                    BeginHold(tool, hit.distance);
                return true;
            }
            if (frame.SelectDown && frame.SelectActor != null)
            {
                var control = collider.GetComponentInParent<ControlTarget>();
                if (control != null)
                    control.TryActivate(frame.SelectActor);
            }
            return true;
        }

        /// <summary>Tracking or input loss: clear hover/dwell and terminate a held grab once.</summary>
        public void Cancel()
        {
            UpdateHover(null);
            UpdateDwell(null, null);
            UpdateUiHover(null);
            if (heldTool == null)
                return;
            var tool = heldTool;
            RestoreHeldBody();
            ClearHeld();
            if (tool != null && tool.IsHeld)
                tool.CancelGrab();
        }

        private void OnDisable() => Cancel();

        private bool RaycastWorld(Ray ray, out RaycastHit nearest)
        {
            nearest = default;
            var count = Physics.RaycastNonAlloc(ray, worldHits, maxDistance, interactionLayers, QueryTriggerInteraction.Collide);
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var candidate = worldHits[i];
                if (heldTool != null && candidate.collider.transform.IsChildOf(heldTool.transform))
                    continue;
                // Socket capture volumes are triggers found on release; they never block pointing.
                if (candidate.collider.isTrigger)
                    continue;
                if (!found || candidate.distance < nearest.distance)
                {
                    nearest = candidate;
                    found = true;
                }
            }
            return found;
        }

        private void UpdateHover(ControlTarget control)
        {
            if (control == hoveredControl)
                return;
            if (hoveredControl != null)
                hoveredControl.SetHovered(false);
            hoveredControl = control;
            if (hoveredControl != null)
                hoveredControl.SetHovered(true);
        }

        private void UpdateDwell(LocatableTarget target, Component actor)
        {
            if (target == dwellTarget && actor == dwellActor)
                return;
            if (dwellTarget != null)
                dwellTarget.EndDwell(dwellActor);
            dwellTarget = null;
            dwellActor = null;
            if (target != null && actor != null && target.BeginDwell(actor))
            {
                dwellTarget = target;
                dwellActor = actor;
            }
        }

        private void UpdateUiHover(Selectable selectable)
        {
            if (selectable == hoveredSelectable)
                return;
            var system = EventSystem.current;
            if (hoveredSelectable != null && system != null)
                ExecuteEvents.Execute(hoveredSelectable.gameObject, new PointerEventData(system), ExecuteEvents.pointerExitHandler);
            hoveredSelectable = selectable;
            if (hoveredSelectable != null && system != null)
                ExecuteEvents.Execute(hoveredSelectable.gameObject, new PointerEventData(system), ExecuteEvents.pointerEnterHandler);
        }

        private static void ClickUi(Selectable selectable, Component actor)
        {
            using (UiActionActor.Push(actor))
            {
                if (selectable is Button button)
                    button.onClick.Invoke();
                else if (selectable is Toggle toggle)
                    toggle.isOn = !toggle.isOn;
            }
            WorldUiRaycast.Invalidate();
        }

        private void BeginHold(GrabbableTool tool, float distance)
        {
            heldTool = tool;
            holdDistance = Mathf.Clamp(distance, minHoldDistance, maxHoldDistance);
            heldBody = tool.GetComponent<Rigidbody>();
            if (heldBody != null)
            {
                heldBodyWasKinematic = heldBody.isKinematic;
                heldBody.isKinematic = true;
            }
        }

        private void ReleaseHeld(Ray ray, Component grabActor)
        {
            var tool = heldTool;
            Physics.SyncTransforms();
            var socket = FindSocket(ray, tool);
            // Restore physics first so a socket records the tool's free-standing state.
            RestoreHeldBody();
            ClearHeld();
            if (!tool.EndGrab(grabActor, socket))
                tool.CancelGrab();
        }

        private PlacementSocket FindSocket(Ray ray, GrabbableTool tool)
        {
            PlacementSocket nearest = null;
            var nearestDistance = float.PositiveInfinity;
            var overlaps = Physics.OverlapSphereNonAlloc(tool.transform.position, socketCaptureRadius, overlapHits, ~0,
                QueryTriggerInteraction.Collide);
            for (var i = 0; i < overlaps; i++)
            {
                var socket = overlapHits[i].GetComponentInParent<PlacementSocket>();
                if (socket == null)
                    continue;
                var distance = (socket.SnapPoint.position - tool.transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = socket;
                    nearestDistance = distance;
                }
            }
            if (nearest != null)
                return nearest;

            var count = Physics.RaycastNonAlloc(ray, worldHits, maxDistance, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                if (worldHits[i].collider.transform.IsChildOf(tool.transform))
                    continue;
                var socket = worldHits[i].collider.GetComponentInParent<PlacementSocket>();
                if (socket != null)
                    return socket;
            }
            return null;
        }

        private void RestoreHeldBody()
        {
            if (heldBody != null)
            {
                heldBody.isKinematic = heldBodyWasKinematic;
                if (!heldBody.isKinematic)
                {
                    heldBody.linearVelocity = Vector3.zero;
                    heldBody.angularVelocity = Vector3.zero;
                }
            }
            heldBody = null;
        }

        private void ClearHeld()
        {
            heldTool = null;
            heldBody = null;
        }
    }
}

using Astra.XR.Interactables;
using UnityEngine;

namespace Astra.XR.Feedback
{
    /// <summary>
    /// Visual-only feedback for a physical control: the cap travels down while pressed and a frame appears while
    /// it is pointed at, so hover and press are shown by shape and motion as well as colour.
    /// Reads the control's state; never activates it or reports anything.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PressFeedback : MonoBehaviour
    {
        [SerializeField] private ControlTarget control;
        [SerializeField] private Transform cap;
        [SerializeField] private GameObject hoverFrame;
        [SerializeField, Range(0f, 0.03f)] private float pressTravel = 0.012f;
        [SerializeField, Min(1f)] private float speed = 18f;

        private Vector3 capRest;
        private float depth;

        public void Configure(ControlTarget target, Transform capTransform, GameObject frame)
        {
            control = target;
            cap = capTransform;
            hoverFrame = frame;
        }

        private void Awake()
        {
            if (cap != null)
                capRest = cap.localPosition;
            if (hoverFrame != null)
                hoverFrame.SetActive(false);
        }

        private void LateUpdate()
        {
            if (control == null)
                return;
            var goal = control.IsPressed ? 1f : 0f;
            depth = Mathf.MoveTowards(depth, goal, Time.unscaledDeltaTime * speed);
            if (cap != null)
            {
                var parentScale = cap.parent != null ? cap.parent.lossyScale.y : 1f;
                var travel = parentScale > 0.0001f ? pressTravel / parentScale : pressTravel;
                cap.localPosition = capRest + Vector3.down * (travel * depth);
            }
            if (hoverFrame != null && hoverFrame.activeSelf != control.IsHovered)
                hoverFrame.SetActive(control.IsHovered);
        }
    }
}

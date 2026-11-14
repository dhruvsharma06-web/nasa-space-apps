using Astra.XR.Rig;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.UI
{
    /// <summary>
    /// One-line caption that tells the cadet how to interact on the current route (phone headset or desktop).
    /// Text only; it never changes input or mission state.
    /// </summary>
    public sealed class ControlsHint : MonoBehaviour
    {
        public const string HeadsetText = "LOOK at a control, then TAP the headset button.  Tap empty space for the menu.";
        public const string DesktopText = "CLICK controls.  HOLD to carry.  RIGHT-DRAG to look.  ESC: menu.  G: phone gaze preview.";
        public const string PreviewText = "PHONE GAZE PREVIEW: RIGHT-DRAG to aim the ring, SPACE to tap.  G to leave.";

        [SerializeField] private Text label;
        [SerializeField] private HeadPoseDriver headPose;
        [SerializeField] private GazeTapInput gaze;

        private string shown;

        public void Configure(Text text, HeadPoseDriver head, GazeTapInput gazeInput)
        {
            label = text;
            headPose = head;
            gaze = gazeInput;
        }

        private void Update()
        {
            if (label == null)
                return;
            var value = headPose != null && headPose.IsTracking ? HeadsetText
                : gaze != null && gaze.DesktopPreview ? PreviewText
                : DesktopText;
            if (value == shown)
                return;
            shown = value;
            label.text = value;
        }
    }
}

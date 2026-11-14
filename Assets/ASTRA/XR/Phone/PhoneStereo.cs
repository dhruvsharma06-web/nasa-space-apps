using UnityEngine;
using UnityEngine.XR;

namespace Astra.XR.Phone
{
    /// <summary>
    /// Fallback split-screen stereo for the gyro "magic window" rig, used only when no XR display is active
    /// (with the Cardboard plugin running, the plugin renders the eyes and lens distortion instead).
    /// Two child eye cameras render the left/right halves, offset by half the IPD. The head camera stays enabled
    /// and tagged MainCamera (other code parents held tools to <c>Camera.main</c>) but renders nothing.
    /// No lens distortion correction: images look slightly pincushioned through the Irusu lenses.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PhoneStereo : MonoBehaviour
    {
        [SerializeField] private bool splitStereo = true;
        [SerializeField] private bool onlyOnMobile = true;
        [SerializeField, Range(0.05f, 0.08f)] private float ipd = 0.063f;
        [SerializeField, Range(50f, 110f)] private float eyeFieldOfView = 85f;

        private Camera head;
        private Camera left;
        private Camera right;
        private int savedMask;
        private CameraClearFlags savedClear;
        private bool active;

        public bool IsActive => active;

        private void Awake() => head = GetComponent<Camera>();

        private void LateUpdate()
        {
            var want = splitStereo && !XRSettings.isDeviceActive && (!onlyOnMobile || Application.isMobilePlatform);
            if (want != active)
                SetActive(want);
        }

        private void OnDisable()
        {
            if (active)
                SetActive(false);
        }

        public void SetActive(bool on)
        {
            active = on;
            if (on)
            {
                if (left == null) left = MakeEye("Eye_Left", -0.5f, new Rect(0f, 0f, 0.5f, 1f));
                if (right == null) right = MakeEye("Eye_Right", 0.5f, new Rect(0.5f, 0f, 0.5f, 1f));
                savedMask = head.cullingMask;
                savedClear = head.clearFlags;
                head.cullingMask = 0;
                head.clearFlags = CameraClearFlags.SolidColor;
                head.depth = -10f;
            }
            else
            {
                head.cullingMask = savedMask == 0 ? ~0 : savedMask;
                head.clearFlags = savedClear;
            }
            if (left != null) left.gameObject.SetActive(on);
            if (right != null) right.gameObject.SetActive(on);
        }

        private Camera MakeEye(string eyeName, float side, Rect rect)
        {
            var go = new GameObject(eyeName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(side * ipd, 0f, 0f);
            var eye = go.AddComponent<Camera>();
            eye.CopyFrom(head);
            eye.cullingMask = head.cullingMask == 0 ? ~0 : head.cullingMask;
            eye.rect = rect;
            eye.fieldOfView = eyeFieldOfView;
            eye.depth = head.depth + 1f;
            eye.tag = "Untagged";
            return eye;
        }
    }
}

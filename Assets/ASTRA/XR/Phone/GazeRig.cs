using Astra.Core.Assessment;
using Astra.XR.Input;
using TMPro;
using UnityEngine;

namespace Astra.XR.Phone
{
    /// <summary>
    /// Assembles the phone gaze rig around a camera: "Recenter Yaw" parent → camera (PhoneHeadPose, PhoneStereo),
    /// a GazeInteractor with a <c>gaze</c> modality actor, and the reticle + hover label. Used by the GazeRig prefab
    /// builder and by the level factory (one call per generated level; see XR/Phone/README.md).
    /// Safe at edit time (scene generation) and at runtime.
    /// </summary>
    public static class GazeRig
    {
        public const string PrefabPath = "Assets/ASTRA/XR/Phone/GazeRig.prefab";
        public const string ReticleMaterialPath = "Assets/ASTRA/XR/Phone/ReticleOverlay.mat";

        /// <param name="camera">The seated eye camera (tagged MainCamera).</param>
        /// <param name="reticleMaterial">Material using ASTRA/ReticleOverlay (pass the asset at edit time so the shader is in the build).</param>
        /// <param name="labelFont">Font for the hover label; defaults to TMP's default font asset.</param>
        /// <param name="keepDesktopMouse">True when a desktop mouse rig stays on the camera (WebGL test build):
        /// the gaze rig then ignores mouse/keyboard taps and mouse look so the two never double-activate or fight.</param>
        public static GazeInteractor AttachTo(Camera camera, Material reticleMaterial = null, TMP_FontAsset labelFont = null,
            bool keepDesktopMouse = false)
        {
            var head = camera.transform;
            var existing = head.GetComponentInParent<GazeInteractor>();
            if (existing != null)
                return existing;

            // Yaw-recenter parent at the eye; the camera keeps its world pose.
            var yaw = new GameObject("Recenter Yaw").transform;
            yaw.SetParent(head.parent, false);
            yaw.position = head.position;
            yaw.rotation = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            head.SetParent(yaw, true);

            var pose = head.gameObject.AddComponent<PhoneHeadPose>();
            pose.Configure(yaw);
            pose.SetMouseLook(!keepDesktopMouse);
            head.gameObject.AddComponent<PhoneStereo>();

            var rig = new GameObject("Gaze Rig").transform;
            rig.SetParent(yaw, false);
            var actor = new GameObject("Gaze Actor (input: gaze)").AddComponent<InputModalityTag>();
            actor.transform.SetParent(rig, false);
            actor.Configure(InputModality.Gaze);

            var reticleObject = new GameObject("Reticle");
            reticleObject.transform.SetParent(rig, false);
            var labelObject = new GameObject("Hover Label");
            labelObject.transform.SetParent(reticleObject.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, -1.9f, 0f);   // reticle units = degrees
            var label = labelObject.AddComponent<TextMeshPro>();
            var font = labelFont != null ? labelFont : TMP_Settings.defaultFontAsset;
            if (font != null) label.font = font;
            label.fontSize = 20f;   // TMP 3D: ~0.1 unit per point → ≈ 1.4° cap height at any distance
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Top;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = new Color(0.97f, 0.98f, 1f);
            label.outlineWidth = 0.22f;
            label.outlineColor = new Color32(0, 0, 0, 220);
            label.rectTransform.sizeDelta = new Vector2(60f, 6f);
            label.text = string.Empty;
            var reticle = reticleObject.AddComponent<GazeReticleView>();
            reticle.Configure(reticleMaterial, label);

            var gaze = rig.gameObject.AddComponent<GazeInteractor>();
            gaze.Configure(head, actor, pose, reticle);
            gaze.SetMouseAndKeyboard(!keepDesktopMouse);
            return gaze;
        }
    }
}

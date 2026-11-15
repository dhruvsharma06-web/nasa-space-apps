using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Astra.App
{
    /// <summary>
    /// Phone tier: on mobile, trims the costliest effects so the headset stays smooth (target ≥ 50 fps):
    /// no realtime reflection probe, no dust particles, lighter bloom, hard shadows, lower render scale.
    /// Desktop/browser keeps full quality. OWNER: Integration Lead.
    /// </summary>
    public class PlatformQuality : MonoBehaviour
    {
        [SerializeField] private bool forceMobileTier;
        public static bool IsMobileTier { get; private set; }

        private void Awake()
        {
            IsMobileTier = forceMobileTier || Application.isMobilePlatform;
            if (!Application.isMobilePlatform)
            {
                // desktop / browser: the mouse rig owns the camera. Laptop orientation sensors would otherwise
                // drive the phone head pose every frame and freeze mouse look.
                foreach (var pose in FindObjectsByType<Astra.XR.Phone.PhoneHeadPose>(FindObjectsSortMode.None)) pose.enabled = false;
                // the mouse crosshair does the gaze job on desktop: no second reticle, no look-to-select surprises
                foreach (var gaze in FindObjectsByType<Astra.XR.Phone.GazeInteractor>(FindObjectsSortMode.None)) gaze.enabled = false;
                foreach (var view in FindObjectsByType<Astra.XR.Phone.GazeReticleView>(FindObjectsSortMode.None)) view.gameObject.SetActive(false);
            }
            if (!IsMobileTier) return;
            foreach (var probe in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) probe.enabled = false;
            var dust = GameObject.Find("Microgravity_Dust");
            if (dust != null) dust.SetActive(false);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.shadows == LightShadows.Soft) l.shadows = LightShadows.Hard;
            foreach (var vol in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (vol.profile != null && vol.profile.TryGet<Bloom>(out var bloom)) bloom.intensity.Override(0.35f);
#if !UNITY_EDITOR   // never modify the shared project asset while testing in the editor
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = Mathf.Min(urp.renderScale, 0.9f);
#endif
        }
    }
}

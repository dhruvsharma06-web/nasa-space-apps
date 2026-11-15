using System;
using System.IO;
using Astra.XR.Rig;
using UnityEngine;
using UnityEngine.SceneManagement;
using VisionAura.XR;

namespace Astra.XR.VisionAura
{
    /// <summary>
    /// Starts the Vision Aura Mac-hosted XR session (iPhone headset) when the app launches.
    /// Compiled only when the com.visionaura.xr package is installed (ASTRA_VISION_AURA), so the canonical
    /// project is unaffected. Mode comes from the build manifest Contents/Resources/VisionAuraXR.json
    /// written by the package's post-build step: "ar" = passthrough (virtual room hidden), otherwise VR.
    /// The desktop window keeps a small status box with request/return controls.
    /// </summary>
    public sealed class VisionAuraSession : MonoBehaviour
    {
        [Serializable]
        private sealed class Manifest
        {
            public string mode;
        }

        private static VisionAuraSession instance;
        private PresentationMode mode = PresentationMode.VR;
        private GUIStyle style;

        public static PresentationMode Mode => instance != null ? instance.mode : PresentationMode.VR;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Application.platform != RuntimePlatform.OSXPlayer || instance != null)
                return;
            var host = new GameObject("ASTRA Vision Aura Session");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<VisionAuraSession>();
            host.AddComponent<VisionAuraHeadsetMode>();
        }

        private void Awake()
        {
            mode = ReadMode();
            Application.targetFrameRate = 60;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            PassthroughHidden.ApplyToLoadedScenes(mode == PresentationMode.AR);
            Debug.Log("[ASTRA] Requesting Vision Aura headset session, mode=" + mode);
            VisionAuraRuntime.Begin(mode);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (instance == this)
                instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode) =>
            PassthroughHidden.ApplyToLoadedScenes(mode == PresentationMode.AR);

        private static PresentationMode ReadMode()
        {
            try
            {
                var path = Path.Combine(Application.dataPath, "Resources", "VisionAuraXR.json");
                if (File.Exists(path))
                {
                    var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
                    if (manifest != null && string.Equals(manifest.mode, "ar", StringComparison.OrdinalIgnoreCase))
                        return PresentationMode.AR;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ASTRA] Could not read VisionAuraXR.json; using VR. " + exception.Message);
            }
            return PresentationMode.VR;
        }

        private void OnGUI()
        {
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            GUI.Box(new Rect(12, 12, 420, 146), "ASTRA Academy • Vision Aura " + (mode == PresentationMode.AR ? "AR" : "VR"));
            GUI.Label(new Rect(22, 34, 400, 40), "Headset: " + VisionAuraRuntime.State +
                (string.IsNullOrEmpty(VisionAuraRuntime.Reason) ? string.Empty : " — " + VisionAuraRuntime.Reason), style);
            if (GUI.Button(new Rect(22, 78, 190, 28), "Request headset"))
                VisionAuraRuntime.Begin(mode);
            if (GUI.Button(new Rect(222, 78, 190, 28), "Return to desktop"))
                VisionAuraRuntime.End();
            // Recenter: button, R key or a headset-button tap; auto-recenter on connect and on every level (remembered).
            var headsetMode = VisionAuraHeadsetMode.Instance;
            if (GUI.Button(new Rect(22, 116, 190, 28), "Recenter (R)") && headsetMode != null)
                headsetMode.Recenter();
            var auto = GUI.Toggle(new Rect(222, 120, 190, 24), VisionAuraHeadsetMode.AutoRecenter, " Auto-recenter");
            if (auto != VisionAuraHeadsetMode.AutoRecenter)
                VisionAuraHeadsetMode.AutoRecenter = auto;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R && headsetMode != null && VisionAuraHeadsetMode.HeadsetActive)
            {
                headsetMode.Recenter();
                e.Use();
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Astra.App.EditorTools
{
    /// <summary>
    /// Renders PNG screenshots of every level scene from the seat (forward / left / right / down) using the GPU.
    /// Batch: Unity -batchmode -projectPath . -executeMethod Astra.App.EditorTools.AstraShotCapture.CaptureBatch -shotsDir &lt;folder&gt;
    /// (no -nographics: it needs a GPU). Editor-only look check; not headset evidence.
    /// OWNER: Integration Lead.
    /// </summary>
    public static class AstraShotCapture
    {
        private static readonly (string name, float yaw, float pitch)[] Views =
        {
            ("front", 0f, 8f), ("left", -55f, 5f), ("right", 55f, 5f), ("down", 0f, 35f),
        };

        [MenuItem("ASTRA/Capture level screenshots")]
        public static void Capture() => CaptureTo(Path.GetFullPath("Builds/Shots"));

        public static void CaptureBatch()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-shotsDir");
                CaptureTo(i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("Builds/Shots"));
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogError(ex); EditorApplication.Exit(1); }
        }

        private static void CaptureTo(string dir)
        {
            Directory.CreateDirectory(dir);
            ShaderUtil.allowAsyncCompilation = false;   // no placeholder shaders in the shots
            foreach (var s in EditorBuildSettings.scenes.Where(s => s.enabled && Path.GetFileName(s.path).StartsWith("L")))
            {
                EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                var cam = Camera.main;
                if (cam == null) { Debug.LogWarning($"[AstraShotCapture] {s.path}: no main camera"); continue; }
                foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.Simulate(5f, true, true);
                foreach (var probe in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) probe.RenderProbe();
                var baseRot = cam.transform.rotation;
                Render(cam, Path.Combine(Path.GetTempPath(), "astra_warmup.png"), 320, 180);   // compile variants first
                foreach (var (name, yaw, pitch) in Views)
                {
                    cam.transform.rotation = Quaternion.Euler(pitch, baseRot.eulerAngles.y + yaw, 0f);
                    string file = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(s.path)}_{name}.png");
                    Render(cam, file, 1600, 900);
                }
                cam.transform.rotation = baseRot;
                Debug.Log($"[AstraShotCapture] {s.path} captured");
            }
        }

        private static void Render(Camera cam, string file, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var active = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

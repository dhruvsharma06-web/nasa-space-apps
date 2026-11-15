using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Astra.App.Tests.PlayMode
{
    /// <summary>Loads the Hub, lets it run, and checks no card has blown up (the old 1250x scale bug); saves a runtime shot.</summary>
    public class RuntimeHubShotTests
    {
        [UnityTest]
        public IEnumerator HubCardsKeepTheirSizeAtRuntime()
        {
            if (!Application.CanStreamedLevelBeLoaded("Hub")) Assert.Ignore("Hub not in build settings");
            SceneManager.LoadScene("Hub");
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.renderMode == RenderMode.WorldSpace)
                    Assert.Less(c.transform.lossyScale.x, 0.01f, $"world canvas '{c.name}' is scaled {c.transform.lossyScale.x}");
            var cam = Camera.main;
            if (cam != null)
            {
                var rt = new RenderTexture(1600, 900, 24);
                cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply(); RenderTexture.active = null;
                Directory.CreateDirectory("Builds/Shots");
                File.WriteAllBytes("Builds/Shots/Hub_runtime.png", tex.EncodeToPNG());
                Object.Destroy(tex); rt.Release();
            }
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }
    }
}

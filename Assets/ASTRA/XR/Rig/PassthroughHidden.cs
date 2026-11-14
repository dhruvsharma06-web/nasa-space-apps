using UnityEngine;

namespace Astra.XR.Rig
{
    /// <summary>
    /// Marks virtual surroundings (room shell, floor, sky) that are hidden when the app runs in
    /// passthrough AR, so only the training desk, controls and panels appear over the real room.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PassthroughHidden : MonoBehaviour
    {
        public static void ApplyToLoadedScenes(bool passthrough)
        {
            for (var s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                    continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var marker in root.GetComponentsInChildren<PassthroughHidden>(true))
                        marker.gameObject.SetActive(!passthrough);
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    {
                        if (!passthrough)
                            continue;
                        camera.clearFlags = CameraClearFlags.SolidColor;
                        camera.backgroundColor = Color.clear;
                    }
                }
            }
        }
    }
}

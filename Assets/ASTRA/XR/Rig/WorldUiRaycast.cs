using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Astra.XR.Rig
{
    /// <summary>
    /// Ray test against world-space UGUI graphics, for tracked pointers that have no screen position.
    /// Returns the nearest raycast-target graphic, which blocks world interaction behind it, and its selectable.
    /// </summary>
    public static class WorldUiRaycast
    {
        private const float RefreshSeconds = 1f;
        private static readonly List<Graphic> Graphics = new List<Graphic>();
        private static readonly List<GameObject> Roots = new List<GameObject>();
        private static float nextRefresh = float.NegativeInfinity;
        private static int cachedFrame = -1;

        public static bool Raycast(Ray ray, float maxDistance, out float distance, out Selectable selectable)
        {
            Refresh();
            distance = float.PositiveInfinity;
            selectable = null;
            Graphic nearest = null;
            for (var i = 0; i < Graphics.Count; i++)
            {
                var graphic = Graphics[i];
                if (graphic == null || !graphic.raycastTarget || !graphic.isActiveAndEnabled)
                    continue;
                var canvas = graphic.canvas;
                if (canvas == null || !canvas.isActiveAndEnabled || canvas.renderMode != RenderMode.WorldSpace)
                    continue;
                var rect = graphic.rectTransform;
                var plane = new Plane(rect.forward, rect.position);
                if (!plane.Raycast(ray, out var hitDistance) || hitDistance < 0f || hitDistance > maxDistance ||
                    hitDistance >= distance)
                    continue;
                Vector2 local = rect.InverseTransformPoint(ray.GetPoint(hitDistance));
                if (!rect.rect.Contains(local))
                    continue;
                distance = hitDistance;
                nearest = graphic;
            }
            if (nearest == null)
                return false;
            var candidate = nearest.GetComponentInParent<Selectable>();
            selectable = candidate != null && candidate.IsInteractable() && candidate.isActiveAndEnabled ? candidate : null;
            return true;
        }

        private static void Refresh()
        {
            if (cachedFrame == Time.frameCount || Time.unscaledTime < nextRefresh)
                return;
            cachedFrame = Time.frameCount;
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            Graphics.Clear();
            for (var s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                    continue;
                scene.GetRootGameObjects(Roots);
                // Inactive graphics are cached too, so a menu is pointable on the frame it opens.
                foreach (var root in Roots)
                    Graphics.AddRange(root.GetComponentsInChildren<Graphic>(true));
            }
        }

        /// <summary>Forces the next query to rescan scenes, e.g. after enabling a menu.</summary>
        public static void Invalidate() => nextRefresh = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Graphics.Clear();
            nextRefresh = float.NegativeInfinity;
            cachedFrame = -1;
        }
    }
}

using Astra.XR.Input;
using TMPro;
using UnityEngine;

namespace Astra.XR.Phone
{
    /// <summary>
    /// Centre reticle for the gaze rig. A small dot sits on the hit point (or <see cref="idleDistance"/> ahead) at a
    /// constant angular size. On a hoverable target it grows into a ring, and a radial fill shows dwell progress
    /// (cyan) or long-press recenter progress (amber). The control's label is shown underneath.
    /// Drawn with <c>ASTRA/ReticleOverlay</c> (ZTest Always), so panels never clip it. Steady: no flashing.
    /// One local unit = 1° of visual angle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GazeReticleView : MonoBehaviour
    {
        private const int Segments = 48;

        [SerializeField] private Material overlayMaterial;
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0.2f)] private float idleDistance = 1.5f;
        [SerializeField] private Color dotColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color ringColor = new Color(0.25f, 0.61f, 1f, 1f);      // #409CFF
        [SerializeField] private Color dwellColor = new Color(0.19f, 0.82f, 0.38f, 1f);  // #30D158
        [SerializeField] private Color holdColor = new Color(1f, 0.7f, 0.25f, 1f);       // #FFB340
        [SerializeField, Range(4f, 40f)] private float growSpeed = 14f;

        private Mesh dotMesh, ringMesh, fillMesh;
        private Transform dot, ring, fill;
        private MeshRenderer ringRenderer, fillRenderer;
        private float ringAmount;
        private float shownFill = -1f;
        private Color shownFillColor;
        private Camera viewCamera;

        public float RingAmount => ringAmount;

        public void Configure(Material material, TMP_Text hoverLabel)
        {
            overlayMaterial = material;
            label = hoverLabel;
        }

        private void Awake()
        {
            if (overlayMaterial == null)
            {
                var shader = Shader.Find("ASTRA/ReticleOverlay") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                    overlayMaterial = new Material(shader) { name = "Reticle (runtime)" };
            }
            dotMesh = Annulus(0f, 0.22f, 1f);
            ringMesh = Annulus(0.62f, 0.85f, 1f);
            fillMesh = new Mesh { name = "Reticle Fill" };
            dot = Part("Dot", dotMesh, dotColor, out _);
            ring = Part("Ring", ringMesh, ringColor, out ringRenderer);
            fill = Part("Fill", fillMesh, dwellColor, out fillRenderer);
            ringRenderer.enabled = false;
            fillRenderer.enabled = false;
            if (label != null)
            {
                label.text = string.Empty;
                var overlay = Shader.Find("TextMeshPro/Distance Field Overlay");
                if (overlay != null && label.fontSharedMaterial != null)
                {
                    var material = new Material(label.fontSharedMaterial) { shader = overlay };
                    label.fontSharedMaterial = material;
                }
            }
        }

        public void Hide()
        {
            if (dot != null) dot.gameObject.SetActive(false);
            if (ring != null) ring.gameObject.SetActive(false);
            if (fill != null) fill.gameObject.SetActive(false);
            if (label != null) label.text = string.Empty;
        }

        /// <param name="dwell">0–1 dwell progress.</param>
        /// <param name="longPress">0–1 long-press (recenter) progress.</param>
        public void Show(Ray ray, bool hasHit, Vector3 hitPoint, IGazeTarget hovered, float dwell, float longPress, string hoverLabel)
        {
            if (dot == null)
                return;
            dot.gameObject.SetActive(true);
            ring.gameObject.SetActive(true);
            fill.gameObject.SetActive(true);

            var distance = hasHit ? Mathf.Max(0.1f, Vector3.Distance(ray.origin, hitPoint) - 0.005f) : idleDistance;
            transform.position = ray.origin + ray.direction * distance;
            if (viewCamera == null) viewCamera = Camera.main;
            var up = viewCamera != null ? viewCamera.transform.up : Vector3.up;
            transform.rotation = Quaternion.LookRotation(ray.direction, up);
            transform.localScale = Vector3.one * (distance * Mathf.Tan(Mathf.Deg2Rad));

            // Dot grows into a ring on a hoverable target (eased, never flashing).
            var goal = hovered != null ? 1f : 0f;
            ringAmount = Mathf.MoveTowards(ringAmount, goal, Time.unscaledDeltaTime * growSpeed * 0.25f);
            var eased = ringAmount * ringAmount * (3f - 2f * ringAmount);
            ringRenderer.enabled = eased > 0.01f;
            ring.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, eased);
            dot.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, eased);

            var holding = longPress > 0.05f;
            var progress = holding ? longPress : dwell;
            var color = holding ? holdColor : dwellColor;
            SetFill(progress, color);

            if (label != null)
            {
                var text = hovered != null ? hoverLabel ?? string.Empty : string.Empty;
                if (label.text != text)
                    label.text = text;
            }
        }

        private void SetFill(float progress, Color color)
        {
            progress = Mathf.Clamp01(Mathf.Round(progress * Segments) / Segments);
            fillRenderer.enabled = progress > 0f;
            if (Mathf.Approximately(progress, shownFill) && color == shownFillColor)
                return;
            shownFill = progress;
            shownFillColor = color;
            if (progress > 0f)
                Annulus(fillMesh, 0.95f, 1.2f, progress, color);
        }

        private Transform Part(string partName, Mesh mesh, Color color, out MeshRenderer renderer)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = overlayMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Tint(mesh, color);
            return go.transform;
        }

        private static void Tint(Mesh mesh, Color color)
        {
            var colors = new Color[mesh.vertexCount];
            for (var i = 0; i < colors.Length; i++) colors[i] = color;
            mesh.colors = colors;
        }

        private static Mesh Annulus(float inner, float outer, float fraction)
        {
            var mesh = new Mesh { name = "Reticle Annulus" };
            Annulus(mesh, inner, outer, fraction, Color.white);
            return mesh;
        }

        /// <summary>Flat ring (or disc when inner = 0) facing -Z, from 12 o'clock clockwise for <paramref name="fraction"/>.</summary>
        private static void Annulus(Mesh mesh, float inner, float outer, float fraction, Color color)
        {
            var steps = Mathf.Max(1, Mathf.CeilToInt(Segments * Mathf.Clamp01(fraction)));
            var vertices = new Vector3[(steps + 1) * 2];
            var colors = new Color[vertices.Length];
            var triangles = new int[steps * 6];
            for (var i = 0; i <= steps; i++)
            {
                var angle = Mathf.PI * 0.5f - fraction * Mathf.PI * 2f * i / steps;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction * outer;
                colors[i * 2] = colors[i * 2 + 1] = color;
            }
            for (var i = 0; i < steps; i++)
            {
                var v = i * 2;
                var t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }
    }
}

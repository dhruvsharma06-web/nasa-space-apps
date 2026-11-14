using UnityEngine;

namespace Astra.XR.Rig
{
    /// <summary>
    /// Gaze cursor drawn as a ring at a constant angular size, so it reads the same near or far on a phone headset.
    /// White ring = nothing tappable; larger cyan ring = a tap will act; amber = holding a tool (tap to drop or place).
    /// The colour change is paired with a size change, so the state does not depend on colour alone.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class GazeReticle : MonoBehaviour
    {
        private const int Segments = 40;

        [SerializeField, Min(0.1f)] private float idleDistance = 1.2f;
        [SerializeField, Range(0.1f, 3f)] private float idleDegrees = 0.55f;
        [SerializeField, Range(0.1f, 3f)] private float targetDegrees = 0.95f;
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color targetColor = new Color(0.3f, 0.9f, 1f, 1f);
        [SerializeField] private Color holdColor = new Color(1f, 0.7f, 0.2f, 1f);

        private readonly Vector3[] points = new Vector3[Segments];
        private LineRenderer line;
        private float pulse;
        private float radiusDegrees;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = true;
            line.positionCount = Segments;
            line.enabled = false;
            radiusDegrees = idleDegrees;
        }

        public void Hide()
        {
            if (line != null)
                line.enabled = false;
        }

        public void Show(Ray ray, bool hasHit, Vector3 hitPoint, bool onTarget, bool holding, bool tapped)
        {
            if (line == null)
                return;
            var distance = hasHit ? Vector3.Distance(ray.origin, hitPoint) : idleDistance;
            // Sit just in front of the surface so the ring is not hidden by what it points at.
            distance = Mathf.Max(0.12f, distance - 0.015f);
            var centre = ray.origin + ray.direction * distance;

            if (tapped)
                pulse = 1f;
            pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * 5f);
            var goal = holding || onTarget ? targetDegrees : idleDegrees;
            radiusDegrees = Mathf.MoveTowards(radiusDegrees, goal, Time.unscaledDeltaTime * 6f);
            var radius = distance * Mathf.Tan((radiusDegrees * (1f - 0.35f * pulse)) * Mathf.Deg2Rad);

            var rotation = Quaternion.LookRotation(ray.direction);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            for (var i = 0; i < Segments; i++)
            {
                var angle = i * Mathf.PI * 2f / Segments;
                points[i] = centre + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
            }
            line.SetPositions(points);
            var width = Mathf.Max(0.0015f, radius * (onTarget || holding ? 0.3f : 0.22f));
            line.startWidth = line.endWidth = width;
            var color = holding ? holdColor : onTarget ? targetColor : idleColor;
            line.startColor = line.endColor = color;
            line.enabled = true;
        }
    }
}

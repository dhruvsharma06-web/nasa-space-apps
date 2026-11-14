using UnityEngine;

namespace Astra.App
{
    /// <summary>Slow "breathing" glow for screen edges and status lights (well under 3 flashes/s, NFR-ACC-01).</summary>
    [RequireComponent(typeof(Renderer))]
    public class EmissionPulse : MonoBehaviour
    {
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        [SerializeField] private Color color = Color.cyan;
        [SerializeField] private float periodSeconds = 3.5f, min = 0.55f, max = 1f;
        private Renderer _renderer;
        private MaterialPropertyBlock _block;

        public void Configure(Color c, float period, float minIntensity, float maxIntensity)
        { color = c; periodSeconds = Mathf.Max(0.5f, period); min = minIntensity; max = maxIntensity; }

        private void Awake() { _renderer = GetComponent<Renderer>(); _block = new MaterialPropertyBlock(); }

        private void Update()
        {
            float k = Mathf.Lerp(min, max, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / periodSeconds));
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(Emission, color * k);
            _renderer.SetPropertyBlock(_block);
        }
    }
}

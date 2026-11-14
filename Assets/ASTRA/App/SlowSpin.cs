using UnityEngine;

namespace Astra.App
{
    /// <summary>Ambient world motion (e.g. Earth turning). Moves the object, never the camera (NFR-UX-01). Pause-independent.</summary>
    public class SlowSpin : MonoBehaviour
    {
        [SerializeField] private Vector3 axis = Vector3.up;
        [SerializeField] private float degreesPerSecond = 0.5f;

        public void Configure(Vector3 spinAxis, float speed) { axis = spinAxis.normalized; degreesPerSecond = speed; }

        private void Update() => transform.Rotate(axis, degreesPerSecond * Time.unscaledDeltaTime, Space.World);
    }
}

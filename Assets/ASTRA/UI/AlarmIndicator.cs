using UnityEngine;

namespace Astra.UI
{
    /// <summary>
    /// Visual alarm + caption. Must be understandable with sound off (FR-APP-04, NFR-ACC-01).
    /// OWNER: XR & Interaction. Signature fixed by CONTRACT.md §7.
    /// </summary>
    public class AlarmIndicator : MonoBehaviour
    {
        public virtual void SetAlarm(bool on, string caption) => Debug.Log($"[Alarm] {(on ? "ON" : "OFF")} {caption}");
    }
}

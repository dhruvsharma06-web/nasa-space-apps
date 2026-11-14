using Astra.Core.Mission;
using UnityEngine;

namespace Astra.UI
{
    /// <summary>Shows which phase the cadet is in. OWNER: XR & Interaction. Signature fixed by CONTRACT.md §7.</summary>
    public class PhaseBanner : MonoBehaviour
    {
        public virtual void Show(ModulePhase phase) => Debug.Log($"[Phase] {phase}");
    }
}

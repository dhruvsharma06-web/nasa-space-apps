using UnityEngine;

namespace Astra.UI
{
    /// <summary>
    /// World-space panel for the current instruction and Practice hints (FR-APP-04).
    /// OWNER: XR & Interaction. Signatures are fixed by CONTRACT.md §7; bodies are stubs until implemented.
    /// </summary>
    public class InstructionPanel : MonoBehaviour
    {
        public virtual void ShowInstruction(string text) => Debug.Log($"[InstructionPanel] {text}");
        public virtual void ShowHint(string text) => Debug.Log($"[InstructionPanel] hint: {text}");
        public virtual void ClearHint() { }
    }
}

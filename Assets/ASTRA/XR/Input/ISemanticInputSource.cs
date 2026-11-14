using System;

namespace Astra.XR.Input
{
    /// <summary>
    /// Adapter boundary between headset-specific input (OpenXR controllers, XR Hands) and mission logic.
    /// One implementation per input route; hand tracking is capability-gated (FR-XR-03/08).
    /// </summary>
    public interface ISemanticInputSource
    {
        string Modality { get; }
        bool IsAvailable { get; }
        event Action<SemanticAction, string /* targetId */> ActionPerformed;
    }
}

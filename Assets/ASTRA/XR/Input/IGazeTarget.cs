namespace Astra.XR.Input
{
    /// Anything the gaze reticle can point at and use.
    public interface IGazeTarget
    {
        string TargetId { get; }
        bool IsGazeEnabled { get; }          // false while a held tool is in hand, etc.
        void SetGazeHover(bool on);          // highlight on/off
        void ActivateFromGaze();             // must raise the contract event with InputModality.Gaze
    }

    /// <summary>
    /// Optional: a "find it" target (a panel with <c>"target": true</c>). Looking at it for
    /// <see cref="GazeLocateSeconds"/> activates it even when dwell-to-select is off, because CONTRACT §3 defines
    /// locating as a 1.0 s gaze dwell. Taps still work as usual.
    /// </summary>
    public interface IGazeLocatable
    {
        float GazeLocateSeconds { get; }
    }

    /// <summary>Optional: the label shown under the reticle while hovering (e.g. "CABIN LIGHTS").</summary>
    public interface IGazeLabel
    {
        string GazeLabel { get; }
    }
}

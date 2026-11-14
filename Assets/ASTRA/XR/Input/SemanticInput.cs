namespace Astra.XR.Input
{
    /// <summary>Platform-neutral interaction events (FR-XR-04). Mission logic only ever sees these.</summary>
    public enum SemanticAction
    {
        Select,
        Grab,
        Release,
        Activate,
        Recenter
    }

    /// <summary>Values written to SessionEvent.input (CONTRACT.md §1).</summary>
    public static class InputModality
    {
        public const string ControllerRay = "controller_ray";
        public const string ControllerGrip = "controller_grip";
        public const string HandPinch = "hand_pinch";
        public const string HandGrab = "hand_grab";
        public const string Simulator = "simulator";
    }
}

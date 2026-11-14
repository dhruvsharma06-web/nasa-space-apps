namespace Astra.Core.Assessment
{
    /// <summary>Allowed values for SessionEvent.event (CONTRACT.md §1). Never use raw strings elsewhere.</summary>
    public static class EventNames
    {
        public const string CalibrationCompleted = "calibration_completed";
        public const string TargetLocated = "target_located";
        public const string TargetSelected = "target_selected";
        public const string ControlActivated = "control_activated";
        public const string ChecklistOpened = "checklist_opened";
        public const string ObjectGrabbed = "object_grabbed";
        public const string ObjectReleased = "object_released";
        public const string ObjectPlaced = "object_placed";
        public const string StatusReported = "status_reported";
        public const string Paused = "paused";
        public const string Resumed = "resumed";
        public const string Recentered = "recentered";

        public static readonly string[] All =
        {
            CalibrationCompleted, TargetLocated, TargetSelected, ControlActivated, ChecklistOpened,
            ObjectGrabbed, ObjectReleased, ObjectPlaced, StatusReported, Paused, Resumed, Recentered
        };

        /// <summary>Events that are "Ignored" (not wrong) when they do not match any step.</summary>
        public static readonly string[] NeutralWhenUnmatched =
        {
            TargetLocated, ObjectGrabbed, ObjectReleased, Paused, Resumed, Recentered
        };
    }

    /// <summary>Allowed values for SessionEvent.input (CONTRACT.md §1).</summary>
    public static class InputModality
    {
        public const string ControllerRay = "controller_ray";
        public const string ControllerGrip = "controller_grip";
        public const string HandPinch = "hand_pinch";
        public const string HandGrab = "hand_grab";
        public const string Simulator = "simulator";
        /// <summary>Phone VR (Cardboard): gaze reticle + tap. CONTRACT v1.4.</summary>
        public const string Gaze = "gaze";

        public static readonly string[] All = { ControllerRay, ControllerGrip, HandPinch, HandGrab, Simulator, Gaze };
    }

    /// <summary>Allowed values for SessionEvent.result (CONTRACT.md §1).</summary>
    public static class EventResults
    {
        public const string Correct = "correct";
        public const string Incorrect = "incorrect";
        public const string Neutral = "neutral";

        public static readonly string[] All = { Correct, Incorrect, Neutral };
    }

    /// <summary>targetId used for app-level events (pause, resume, recenter) that are not tied to an object.</summary>
    public static class SystemTargets
    {
        public const string System = "system";
        public const string Calibration = "calibration";
    }
}

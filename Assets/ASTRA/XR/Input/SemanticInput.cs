namespace Astra.XR.Input
{
    /// <summary>
    /// Platform-neutral interaction actions (FR-XR-04). Mapping to contract event names is in CONTRACT.md §3.
    /// Input modality strings live in Astra.Core.Assessment.InputModality.
    /// </summary>
    public enum SemanticAction
    {
        Select,
        Grab,
        Release,
        Activate,
        Recenter
    }
}

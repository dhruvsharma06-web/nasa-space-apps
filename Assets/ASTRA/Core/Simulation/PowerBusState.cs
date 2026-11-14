namespace Astra.Core.Simulation
{
    /// <summary>Simplified, fictional electrical model for the MVP power fault (FR-SIM-03).</summary>
    public enum PowerBusState
    {
        Nominal,
        Faulted,
        Isolated,
        Restored
    }
}

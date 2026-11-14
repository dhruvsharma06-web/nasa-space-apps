using Astra.Levels;
using Astra.Core.Simulation;

/// <summary>
/// L7 Integrated Mission: the same power-fault drama as L4, with L7's controls (the BUS B selector and the
/// CROSS-TIE breaker). The level factory attaches it by name and fills its fields. OWNER: Core Logic (L7).
/// </summary>
public class L7FinaleView : PowerFaultLevelView
{
    protected override string IsolateTargetId => PowerBusSimulation.DefaultIsolateTargetId;   // power_bus_b_selector
    protected override string CrossTieTargetId => PowerBusSimulation.DefaultCrossTieTargetId; // crosstie_breaker
}

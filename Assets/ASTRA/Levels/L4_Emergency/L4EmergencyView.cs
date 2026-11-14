using Astra.Levels;

/// <summary>
/// L4 Emergency Response: the power-fault drama (alarm, dimmed cabin, bus lights, STATUS) for the generated scene.
/// The level factory attaches it by name and fills its fields. OWNER: Core Logic (L4).
/// </summary>
public class L4EmergencyView : PowerFaultLevelView
{
    protected override string IsolateTargetId => "power_bus_b_dial";
    protected override string CrossTieTargetId => "crosstie_lever";
}

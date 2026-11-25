using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether the server tells clients where air is rushing, so they can draw fog, dust and haze in the flow.
    /// </summary>
    public static readonly CVarDef<bool> AirFlowVisualsEnabled =
        CVarDef.Create("air_flow_visuals.enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// How many tiles away from a breach the fog, dust and haze are still drawn. They fade out towards this distance.
    /// </summary>
    public static readonly CVarDef<int> AirFlowVisualsRadius =
        CVarDef.Create("air_flow_visuals.radius", 5, CVar.SERVER | CVar.REPLICATED);
}

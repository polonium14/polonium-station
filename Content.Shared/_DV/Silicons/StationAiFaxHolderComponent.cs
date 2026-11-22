using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Silicons;

/// <summary>
/// Gives a station AI its fax module and the action that opens it.
/// Comes and goes with the rest of the AiHeld components when the AI enters or leaves a core.
/// </summary>
[RegisterComponent]
public sealed partial class StationAiFaxHolderComponent : Component
{
    public const string Container = "station_ai_fax_module";

    /// <summary>
    /// Fax module spawned the first time this AI enters a core.
    /// </summary>
    [DataField]
    public EntProtoId Module = "StationAiFaxModule";

    /// <summary>
    /// Action that toggles the fax window.
    /// </summary>
    [DataField]
    public EntProtoId Action = "ActionStationAiOpenFax";

    [DataField]
    public EntityUid? ActionEntity;
}

public sealed partial class StationAiOpenFaxEvent : InstantActionEvent;

using Content.Shared.Whitelist;

namespace Content.Server._Polonium.Objectives;

/// <summary>
/// Counts other characters whose current body passes the whitelist.
/// The goal comes from NumberObjective.
/// </summary>
[RegisterComponent, Access(typeof(ObjectiveConditionsSystem))]
public sealed partial class MindWhitelistCountConditionComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public bool RequireAlive = true;
}

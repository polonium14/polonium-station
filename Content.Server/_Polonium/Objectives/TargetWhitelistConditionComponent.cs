using Content.Shared.Whitelist;

namespace Content.Server._Polonium.Objectives;

/// <summary>
/// The target's current body has to pass the whitelist, e.g. be a cluwne or a polymorphed animal.
/// </summary>
[RegisterComponent, Access(typeof(ObjectiveConditionsSystem))]
public sealed partial class TargetWhitelistConditionComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public bool RequireAlive = true;
}

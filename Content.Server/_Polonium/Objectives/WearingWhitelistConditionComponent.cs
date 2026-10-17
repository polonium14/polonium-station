using Content.Shared.Whitelist;

namespace Content.Server._Polonium.Objectives;

/// <summary>
/// The player has to be alive and wear an item passing the whitelist in every slot.
/// </summary>
[RegisterComponent, Access(typeof(ObjectiveConditionsSystem))]
public sealed partial class WearingWhitelistConditionComponent : Component
{
    [DataField(required: true)]
    public List<string> Slots = new();

    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();
}

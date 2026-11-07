using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._EinsteinEngines.StepTrigger;

/// <summary>
/// The entity does not set off step triggers that pass the whitelist.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StepTriggerImmuneComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();
}

using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._EinsteinEngines.StepTrigger;

/// <summary>
/// The entity does not set off step triggers that pass the whitelist.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StepTriggerImmuneComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public EntityWhitelist Whitelist = new();
}

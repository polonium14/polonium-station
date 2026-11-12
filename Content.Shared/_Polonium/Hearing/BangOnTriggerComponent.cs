using Content.Shared.EntityEffects;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Hearing;

/// <summary>
/// Deafening bang on trigger, e.g. a flashbang. Works like a changeling screech: everything in range that can hear it
/// gets the effects, unless it wears something with <see cref="Content.Shared.Screech.NoiseProtectionComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BangOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField, AutoNetworkedField]
    public float Range = 7f;

    /// <summary>
    /// Whether walls stop the bang. Without it the bang goes through everything, like the changeling screech.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequireUnoccluded = true;

    [DataField]
    public List<EntityEffect> Effects = [];

    [DataField]
    public LocId? HitPopup = "bang-on-trigger-hit";

    [DataField]
    public LocId? ProtectedPopup = "bang-on-trigger-protected";
}

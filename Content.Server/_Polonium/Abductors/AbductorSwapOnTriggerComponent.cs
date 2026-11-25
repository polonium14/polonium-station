using Content.Shared.Trigger.Components.Effects;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// Swaps the target's place with a random living mob nearby.
/// </summary>
[RegisterComponent]
public sealed partial class AbductorSwapOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField]
    public float Range = 8f;

    [DataField]
    public LocId Popup = "abductor-gland-popup-swap";
}

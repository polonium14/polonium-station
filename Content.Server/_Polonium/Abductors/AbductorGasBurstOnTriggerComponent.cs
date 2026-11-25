using Content.Shared.Atmos;
using Content.Shared.Trigger.Components.Effects;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// Adds a portion of gas to the air around the target every time it triggers.
/// </summary>
[RegisterComponent]
public sealed partial class AbductorGasBurstOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField(required: true)]
    public Gas Gas;

    [DataField]
    public float Moles = 8f;
}

using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// Shoots lightning from the target at whatever is near it, never at the target itself.
/// </summary>
[RegisterComponent]
public sealed partial class AbductorArcsOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField]
    public float Range = 4f;

    [DataField]
    public int Bolts = 2;

    [DataField]
    public EntProtoId Lightning = "LightningAbductorGland";
}

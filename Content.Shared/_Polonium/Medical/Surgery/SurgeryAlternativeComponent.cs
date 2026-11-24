using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Medical.Surgery;

/// <summary>
/// Marks a surgery as another way to do one of the listed surgeries. The surgery window puts it
/// on the same row as that surgery, half the width each, under a short label.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryAlternativeComponent : Component
{
    /// <summary>
    /// The surgeries this one stands next to; the first of them that is on the list is used.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Of = [];

    /// <summary>
    /// What the half-width button says instead of the surgery's full name.
    /// </summary>
    [DataField(required: true)]
    public LocId Label;
}

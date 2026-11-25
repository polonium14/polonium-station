using Content.Shared._Polonium.Abductors;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// The Mothership's request for this specimen, with the organ it wants replaced.
/// </summary>
[RegisterComponent, Access(typeof(AbductorOrderSystem))]
public sealed partial class AbductorOrderComponent : Component
{
    [ViewVariables]
    public ProtoId<AbductorOrderPrototype> Order;

    [ViewVariables]
    public EntityUid Original;
}

using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Construction.Events;

public sealed partial class XenoPlantWeedsActionEvent : InstantActionEvent
{
    [DataField]
    public FixedPoint2 PlasmaCost = 90;

    [DataField]
    public EntProtoId? Prototype;
}

using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.ResinSurge;

[Serializable, NetSerializable]
public sealed partial class ResinSurgeStickyResinDoafter : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates Coordinates;

    [DataField]
    public FixedPoint2 PlasmaCost;

    public ResinSurgeStickyResinDoafter(NetCoordinates coordinates, FixedPoint2 plasmaCost)
    {
        Coordinates = coordinates;
        PlasmaCost = plasmaCost;
    }
}

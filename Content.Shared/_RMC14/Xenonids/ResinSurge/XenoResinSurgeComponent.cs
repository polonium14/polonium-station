using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.ResinSurge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoResinSurgeSystem))]
public sealed partial class XenoResinSurgeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId StickyResinId = "XenoStickyResinWeak";

    [DataField, AutoNetworkedField]
    public EntProtoId UnstableWallId = "WallXenoResinWeak";

    [DataField, AutoNetworkedField]
    public int StickyResinRadius = 1;

    [DataField, AutoNetworkedField]
    public TimeSpan StickyResinDoAfterPeriod = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan SuccessCooldown = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 100;

    [DataField, AutoNetworkedField]
    public float Range = 7f;

    [DataField]
    public DoAfterId? ResinDoAfter;
}

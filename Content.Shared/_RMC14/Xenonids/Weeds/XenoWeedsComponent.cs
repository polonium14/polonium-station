using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Weeds;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoWeedsSystem))]
public sealed partial class XenoWeedsComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Range = 4;

    [DataField, AutoNetworkedField]
    public EntProtoId Spawns = "XenoWeeds";

    [DataField, AutoNetworkedField]
    public bool Spreads = true;

    [DataField, AutoNetworkedField]
    public bool IsSource = true;

    [DataField, AutoNetworkedField]
    public EntityUid? Source;

    [DataField, AutoNetworkedField]
    public List<EntityUid> Spread = new();

    [DataField, AutoNetworkedField]
    public TimeSpan MinRandomDelete = TimeSpan.FromSeconds(9);

    [DataField, AutoNetworkedField]
    public TimeSpan MaxRandomDelete = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public float SpeedMultiplierXeno = 1f;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplierOutsider = 0.3714f;
}

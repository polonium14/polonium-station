using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Hive;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoHiveSystem))]
public sealed partial class HiveComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? CurrentQueen;
}

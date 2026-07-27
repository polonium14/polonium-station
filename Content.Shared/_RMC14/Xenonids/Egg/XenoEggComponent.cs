using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Egg;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoEggSystem))]
public sealed partial class XenoEggComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId SpawnPrototype = "CMXenoLarva";

    [DataField, AutoNetworkedField]
    public bool Grown;

    [DataField, AutoNetworkedField]
    public TimeSpan GrowTime = TimeSpan.FromSeconds(15);

    [DataField, AutoNetworkedField]
    public TimeSpan NextGrow;

    [DataField, AutoNetworkedField]
    public TimeSpan HatchDelay = TimeSpan.FromSeconds(45);

    [DataField, AutoNetworkedField]
    public TimeSpan NextHatch;
}

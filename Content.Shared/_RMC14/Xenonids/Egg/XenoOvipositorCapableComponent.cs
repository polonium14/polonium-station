using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Egg;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoEggSystem))]
public sealed partial class XenoOvipositorCapableComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan LayDelay = TimeSpan.FromSeconds(30);

    [DataField, AutoNetworkedField]
    public EntProtoId EggPrototype = "XenoEgg";
}

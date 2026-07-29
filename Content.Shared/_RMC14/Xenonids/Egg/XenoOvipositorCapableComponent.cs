using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Egg;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(XenoEggSystem))]
public sealed partial class XenoOvipositorCapableComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan LayDelay = TimeSpan.FromSeconds(35);

    [DataField, AutoNetworkedField]
    public EntProtoId EggPrototype = "XenoEgg";

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? NextLayEgg;

    [DataField, AutoNetworkedField]
    public bool LayReadyNotified = true;
}

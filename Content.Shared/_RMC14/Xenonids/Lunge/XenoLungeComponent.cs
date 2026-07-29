using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Lunge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoLungeSystem))]
public sealed partial class XenoLungeComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 5f;

    [DataField, AutoNetworkedField]
    public float ThrowSpeed = 30f;

    [DataField, AutoNetworkedField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(4);
}

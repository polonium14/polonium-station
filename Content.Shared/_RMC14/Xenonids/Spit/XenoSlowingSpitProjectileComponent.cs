using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Spit;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSpitSystem))]
public sealed partial class XenoSlowingSpitProjectileComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Paralyze = TimeSpan.FromSeconds(3.5);
}

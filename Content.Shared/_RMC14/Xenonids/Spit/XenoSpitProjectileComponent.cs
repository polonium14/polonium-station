using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Spit;

[RegisterComponent, NetworkedComponent]
public sealed partial class XenoSpitProjectileComponent : Component
{
    [DataField]
    public bool DeleteOnFriendlyXeno = true;
}

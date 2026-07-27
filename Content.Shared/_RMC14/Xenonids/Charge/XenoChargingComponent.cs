using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Charge;

[RegisterComponent, NetworkedComponent]
[Access(typeof(XenoChargeSystem))]
public sealed partial class XenoChargingComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new();

    public HashSet<EntityUid> HitEntities = new();
}

using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Acid;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedXenoAcidSystem))]
public sealed partial class DamageableCorrodingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? AcidPrototype;

    [DataField, AutoNetworkedField]
    public EntityUid? Acid;

    [DataField, AutoNetworkedField]
    public float Dps = 8f;

    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan CorrodesAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextDamageAt;

    [DataField, AutoNetworkedField]
    public XenoAcidStrength Strength = XenoAcidStrength.Normal;
}

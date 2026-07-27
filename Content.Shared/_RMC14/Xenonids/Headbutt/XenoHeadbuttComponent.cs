using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Headbutt;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoHeadbuttSystem))]
public sealed partial class XenoHeadbuttComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = { ["Blunt"] = 30 }
    };

    [DataField, AutoNetworkedField]
    public DamageSpecifier CrestedDamageReduction = new()
    {
        DamageDict = { ["Blunt"] = -10 }
    };

    // self throw distance - not the knockback
    [DataField, AutoNetworkedField]
    public float Range = 3.5f;

    // knockback on hit
    [DataField, AutoNetworkedField]
    public float ThrowForce = 2f;

    [DataField, AutoNetworkedField]
    public float CrestFortifiedThrowAdd = 1f;

    [DataField, AutoNetworkedField]
    public float ThrowSpeed = 10f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_claw_block.ogg");

    [DataField, AutoNetworkedField]
    public EntProtoId Effect = "CMEffectPunch";

    [DataField, AutoNetworkedField]
    public Vector2? Charge;
}

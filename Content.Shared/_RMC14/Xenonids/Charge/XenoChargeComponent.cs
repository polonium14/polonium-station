using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using System.Numerics;

namespace Content.Shared._RMC14.Xenonids.Charge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoChargeSystem))]
public sealed partial class XenoChargeComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 20;

    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = { ["Blunt"] = 40 }
    };

    // how far the throw impulse reaches
    [DataField, AutoNetworkedField]
    public float Range = 8f;

    [DataField, AutoNetworkedField]
    public float SlowRange = 1.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan SlowTime = TimeSpan.FromSeconds(3.5);

    [DataField, AutoNetworkedField]
    public float SlowMultiplier = 0.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan ChargeDelay = TimeSpan.FromSeconds(1.2);

    [DataField, AutoNetworkedField]
    public float Strength = 20f;

    [DataField, AutoNetworkedField]
    public float Knockback = 2f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_claw_block.ogg");

    [DataField, AutoNetworkedField]
    public Vector2? Charge;
}

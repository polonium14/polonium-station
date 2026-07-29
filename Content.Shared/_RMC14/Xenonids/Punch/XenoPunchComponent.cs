using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Punch;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoPunchSystem))]
public sealed partial class XenoPunchComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = { ["Blunt"] = 27.5 }
    };

    [DataField, AutoNetworkedField]
    public float Range = 1.5f;

    [DataField, AutoNetworkedField]
    public float ThrowRange = 1f;

    [DataField, AutoNetworkedField]
    public float ThrowSpeed = 10f;

    [DataField, AutoNetworkedField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public float SlowMultiplier = 0.55f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_claw_block.ogg");
}

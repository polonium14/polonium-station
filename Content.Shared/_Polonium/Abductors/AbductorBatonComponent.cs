using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Abductors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(AbductorBatonSystem))]
public sealed partial class AbductorBatonComponent : Component
{
    [DataField, AutoNetworkedField]
    public AbductorBatonMode Mode = AbductorBatonMode.Stun;

    [DataField]
    public float StunStamina = 60f;

    [DataField]
    public TimeSpan SleepDuration = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan CuffDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan MarkDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public EntProtoId Cuffs = "Cablecuffs";

    [DataField]
    public SoundSpecifier? ModeSound;
}

[Serializable, NetSerializable]
public enum AbductorBatonMode : byte
{
    Stun,
    Sleep,
    Cuff,
    Mark,
}

[Serializable, NetSerializable]
public enum AbductorBatonVisuals : byte
{
    Mode,
}

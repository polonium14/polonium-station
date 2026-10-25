using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Xenonids;

/// <summary>
/// Facehugger z NSV13/tg: przedmiot-maska, który skacze na twarz sąsiadującej ofiary,
/// usypia ją i po chwili składa w niej embrion.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedFacehuggerSystem))]
public sealed partial class FacehuggerComponent : Component
{
    [DataField, AutoNetworkedField]
    public FacehuggerState State = FacehuggerState.Active;

    /// <summary>
    /// Lamarr: skacze i obejmuje twarz, ale nikogo nie zapładnia ani nie usypia.
    /// </summary>
    [DataField]
    public bool Sterile;

    /// <summary>
    /// Na kogo właśnie się przyczepił. Embrion powstaje tylko wtedy, gdy facehugger nadal jest na twarzy tej osoby.
    /// </summary>
    [DataField]
    public EntityUid? Victim;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? ImpregnateAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? WakeAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextCheck;

    [DataField]
    public TimeSpan MinImpregnationTime = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan MaxImpregnationTime = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Ile czasu nieaktywny facehugger leży, zanim znów zacznie polować.
    /// </summary>
    [DataField]
    public TimeSpan MinIdleTime = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan MaxIdleTime = TimeSpan.FromSeconds(40);

    /// <summary>
    /// Jak długo ofiara śpi po przyczepieniu (w NSV13 50 s nieprzytomności).
    /// </summary>
    [DataField]
    public TimeSpan KnockoutTime = TimeSpan.FromSeconds(50);

    [DataField]
    public float LeapRange = 1.5f;

    /// <summary>
    /// Powyżej tej temperatury otoczenia facehugger ginie.
    /// </summary>
    [DataField]
    public float MaxTemperature = 300f;

    /// <summary>
    /// Tyle obrażeń zabija facehuggera (w NSV13 spadek integralności poniżej 90 ze 100).
    /// </summary>
    [DataField]
    public float DeathDamage = 10f;

    [DataField]
    public float DamageTaken;
}

[Serializable, NetSerializable]
public enum FacehuggerState : byte
{
    Active,
    Idle,
    Dead,
    Impregnated,
}

[Serializable, NetSerializable]
public enum FacehuggerVisuals : byte
{
    State,
}

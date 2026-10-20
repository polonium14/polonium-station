using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Xenonids;

/// <summary>
/// Embrion ksenomorfa. Jest organem w ciele nosiciela, więc wycina się go zwykłą operacją Shitmed.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class XenoEmbryoComponent : Component
{
    [DataField]
    public int Stage;

    [DataField]
    public int MaxStage = 5;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextStageAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTick;

    [DataField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan FirstStageTime = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan MinStageTime = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan MaxStageTime = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Każdy żywy dorosły ksenomorf z graczem wydłuża kolejny etap o tyle.
    /// </summary>
    [DataField]
    public TimeSpan PerXenoStageTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan MaxPerXenoStageTime = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Szansa na wyklucie przy każdym tiku na ostatnim etapie.
    /// </summary>
    [DataField]
    public float BurstChance = 0.5f;

    [DataField]
    public EntProtoId LarvaPrototype = "CMXenoLarva";
}

/// <summary>
/// Nosiciel embrionu. Służy do odporności na kolejne facehuggery i do ikonki widocznej dla ula i medycznego HUD-a.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoInfectedComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stage;

    [DataField, AutoNetworkedField]
    public EntityUid? Embryo;

    [DataField]
    public List<ProtoId<FactionIconPrototype>> StageIcons = new()
    {
        "XenoInfectedIcon0",
        "XenoInfectedIcon1",
        "XenoInfectedIcon2",
        "XenoInfectedIcon3",
        "XenoInfectedIcon4",
        "XenoInfectedIcon5",
    };
}

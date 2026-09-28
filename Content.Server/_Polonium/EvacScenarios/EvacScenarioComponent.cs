using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.EvacScenarios;

[RegisterComponent, AutoGenerateComponentPause, Access(typeof(EvacScenarioSystem))]
public sealed partial class EvacScenarioComponent : Component
{
    /// <summary>
    /// How long the shuttles fly through hyperspace as usual before the scenario starts.
    /// </summary>
    [DataField]
    public TimeSpan StartDelay;

    [DataField]
    public float Chance;

    [DataField]
    public List<EvacScenarioRuleChance> RuleChances = new();

    [DataField]
    public TimeSpan? FlightDuration;

    [DataField]
    public SoundSpecifier? Music;

    [ViewVariables]
    public ResolvedSoundSpecifier? ResolvedMusic;

    [DataField]
    public List<EvacScenarioAnnouncement> Announcements = new();

    [DataField]
    public HashSet<EntityUid> Shuttles = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? DepartedAt;

    /// <summary>
    /// Whether the scenario has started and its music is playing.
    /// </summary>
    [DataField]
    public bool Started;

    [DataField]
    public int NextAnnouncement;
}

[DataDefinition]
public sealed partial class EvacScenarioRuleChance
{
    [DataField(required: true)]
    public List<EntProtoId> Rules = new();

    [DataField(required: true)]
    public float Chance;
}

[DataDefinition]
public sealed partial class EvacScenarioAnnouncement
{
    /// <summary>
    /// Moment of the scenario the message is sent at.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan Time;

    [DataField(required: true)]
    public LocId Message;

    [DataField]
    public LocId? Sender;

    [DataField]
    public bool ShuttleOnly;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public Color? Color;
}

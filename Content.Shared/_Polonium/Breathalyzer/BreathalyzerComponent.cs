using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Breathalyzer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(BreathalyzerSystem))]
public sealed partial class BreathalyzerComponent : Component
{
    [DataField]
    public TimeSpan BlowDuration = TimeSpan.FromSeconds(2.5);

    [DataField]
    public TimeSpan AnalysisDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Reagents counted as alcohol in the blood and metabolites of the tested entity.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> Reagents = new() { "Ethanol" };

    /// <summary>
    /// How many per mille one unit of alcohol in the body adds to the reading.
    /// </summary>
    [DataField]
    public float PerMillePerUnit = 0.2f;

    /// <summary>
    /// How many per mille one second of remaining drunkenness adds to the reading.
    /// Alcohol burns off long before drunkenness wears off, so the status keeps the reading in line with what players see.
    /// </summary>
    [DataField]
    public float PerMillePerDrunkSecond = 0.01f;

    /// <summary>
    /// The upper end of the scale shown on the display.
    /// </summary>
    [DataField]
    public float ScaleMax = 4f;

    /// <summary>
    /// Result levels sorted by ascending threshold. A reading belongs to the last level whose threshold it reaches.
    /// </summary>
    [DataField(required: true)]
    public List<BreathalyzerLevel> Levels = new();

    /// <summary>
    /// Battery charge used by one test. A medium power cell (720) lasts for 17 tests.
    /// </summary>
    [DataField]
    public float ChargePerTest = 42f;

    /// <summary>
    /// Battery charge used by printing one report.
    /// </summary>
    [DataField]
    public float ChargePerPrint = 14f;

    /// <summary>
    /// The lowest real reading in per mille at which the tested entity may vomit into the mouthpiece.
    /// </summary>
    [DataField]
    public float VomitThreshold = 3f;

    /// <summary>
    /// The chance that a test at or above <see cref="VomitThreshold"/> ends with vomiting.
    /// </summary>
    [DataField]
    public float VomitChance = 0.2f;

    [DataField]
    public EntProtoId ReportPaper = "BreathalyzerReportPaper";

    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier? PrintSound;

    [DataField]
    public SoundSpecifier? StartSound;

    [DataField]
    public SoundSpecifier? BlowSound;

    [DataField]
    public SoundSpecifier? AnalyzeSound;

    [DataField]
    public string IdleScreen = "screen-idle";

    [DataField]
    public string BlowScreen = "screen-blow";

    [DataField]
    public string AnalyzeScreen = "screen-analyze";

    [DataField]
    public string OffScreen = "screen-off";

    [DataField, AutoNetworkedField]
    public BreathalyzerState State = BreathalyzerState.Idle;

    [DataField, AutoNetworkedField]
    public float? LastResult;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan AnalysisEnd;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan PrintReadyAt;

    /// <summary>
    /// Set when someone vomits into the mouthpiece. The device refuses tests until it is cleaned with soap.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool MouthpieceDirty;

    [DataField]
    public string? LastSubjectName;

    /// <summary>
    /// Round time of the last finished measurement, printed on the report.
    /// </summary>
    [DataField]
    public TimeSpan LastMeasuredRoundTime;

    [ViewVariables]
    public float PendingResult;

    [ViewVariables]
    public string? PendingSubjectName;

    [ViewVariables]
    public EntityUid? BlowStream;
}

[DataDefinition]
public sealed partial class BreathalyzerLevel
{
    /// <summary>
    /// The lowest reading in per mille that belongs to this level.
    /// </summary>
    [DataField]
    public float Threshold;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public Color Color;

    /// <summary>
    /// The sprite state of the device's screen while it shows a result of this level.
    /// </summary>
    [DataField(required: true)]
    public string Screen = string.Empty;

    [DataField]
    public SoundSpecifier? Sound;
}

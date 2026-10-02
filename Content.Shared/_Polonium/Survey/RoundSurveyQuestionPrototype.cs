using Content.Shared.Roles;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Survey;

/// <summary>
/// A question players may get at the end of a round, answered on a scale from
/// <see cref="MinAnswer"/> to <see cref="MaxAnswer"/>.
/// </summary>
[Prototype]
public sealed partial class RoundSurveyQuestionPrototype : IPrototype
{
    public const int MinAnswer = 1;
    public const int MaxAnswer = 5;

    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Text;

    /// <summary>
    /// What the lowest answer stands for.
    /// </summary>
    [DataField(required: true)]
    public LocId Low;

    /// <summary>
    /// What the highest answer stands for.
    /// </summary>
    [DataField(required: true)]
    public LocId High;

    /// <summary>
    /// The answer that counts as ideal on a two-sided scale. Game presets can override it.
    /// If null, higher is simply better.
    /// </summary>
    [DataField]
    public float? Target;

    /// <summary>
    /// Only the two ends of the scale can be picked: <see cref="Low"/> is stored as <see cref="MinAnswer"/>
    /// and <see cref="High"/> as <see cref="MaxAnswer"/>.
    /// </summary>
    [DataField]
    public bool YesNo;

    [DataField]
    public RoundSurveyAudience Audience = RoundSurveyAudience.Everyone;

    /// <summary>
    /// If any of <see cref="Jobs"/>, <see cref="Departments"/> and <see cref="Antags"/> is set,
    /// the question is only for players who fit at least one of them.
    /// </summary>
    [DataField]
    public List<ProtoId<JobPrototype>>? Jobs;

    [DataField]
    public List<ProtoId<DepartmentPrototype>>? Departments;

    [DataField]
    public List<ProtoId<AntagPrototype>>? Antags;

    /// <summary>
    /// First day the question is asked, by UTC. If null, it is asked from the start.
    /// </summary>
    [DataField]
    public DateTime? From;

    /// <summary>
    /// Last day the question is asked, by UTC. If null, it is asked forever.
    /// </summary>
    [DataField]
    public DateTime? Until;

    /// <summary>
    /// Asked every round. Other questions share the slots that are left, picked by <see cref="Weight"/>.
    /// </summary>
    [DataField]
    public bool Always;

    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// A player who got this question does not get it again in this many of their next surveys.
    /// Does not apply to questions that are <see cref="Always"/> asked.
    /// </summary>
    [DataField]
    public int Cooldown = 1;

    /// <summary>
    /// How long the player must have been in the round to get this question.
    /// </summary>
    [DataField]
    public TimeSpan MinTimeInRound = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Only asked if somebody was an antagonist this round.
    /// </summary>
    [DataField]
    public bool NeedsAntags;

    /// <summary>
    /// Only asked if a game rule fitting the whitelist is running when the round ends.
    /// </summary>
    [DataField]
    public EntityWhitelist? RuleWhitelist;

    /// <summary>
    /// Position in the survey and in the summary, lowest first.
    /// </summary>
    [DataField]
    public int Order;
}

public enum RoundSurveyAudience : byte
{
    Everyone,

    /// <summary>
    /// Players with a job who were not antagonists.
    /// </summary>
    Crew,

    Antag,
}

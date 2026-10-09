using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Survey;

/// <summary>
/// Something a player can tick to explain an answer, offered by a <see cref="RoundSurveyFollowUp"/>.
/// </summary>
[Prototype]
public sealed partial class RoundSurveyReasonPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;
}

/// <summary>
/// Asks the player to explain an answer that falls between <see cref="From"/> and <see cref="To"/>.
/// </summary>
[DataDefinition]
public sealed partial class RoundSurveyFollowUp
{
    [DataField(required: true)]
    public LocId Text;

    [DataField(required: true)]
    public int From;

    [DataField(required: true)]
    public int To;

    /// <summary>
    /// What the player can tick, in the order it is shown.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<RoundSurveyReasonPrototype>> Reasons = new();
}

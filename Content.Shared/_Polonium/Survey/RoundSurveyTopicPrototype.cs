using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Survey;

/// <summary>
/// What a comment written by a player is about.
/// </summary>
[Prototype]
public sealed partial class RoundSurveyTopicPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Position in the list of topics
    /// </summary>
    [DataField]
    public int Order;
}

using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Survey;

/// <summary>
/// A range of round sizes that the weekly survey digest reports on separately.
/// It covers rounds from <see cref="Min"/> players up to where the next group starts.
/// </summary>
[Prototype]
public sealed partial class RoundSurveyPlayerGroupPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public int Min;
}

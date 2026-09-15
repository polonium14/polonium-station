namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Inverts whatever it wraps. "Not wearing a suit", "internals off", and so on.</summary>
public sealed partial class NotCondition : TutorialCondition
{
    [DataField(required: true)]
    public TutorialCondition Condition = default!;
}

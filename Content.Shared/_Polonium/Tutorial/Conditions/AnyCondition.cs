namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class AnyCondition : TutorialCondition
{
    [DataField(required: true)]
    public List<TutorialCondition> Conditions = new();
}

namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class AllCondition : TutorialCondition
{
    [DataField(required: true)]
    public List<TutorialCondition> Conditions = new();
}

namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class DeadAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

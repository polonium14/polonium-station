namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class MeleeHitAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

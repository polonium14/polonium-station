namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class PoweredAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

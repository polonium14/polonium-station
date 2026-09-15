namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class BothHandsAnchorsCondition : TutorialCondition
{
    [DataField(required: true)]
    public List<string> AnchorIds = new();
}

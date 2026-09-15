namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class AnyReachAnchorsCondition : TutorialCondition
{
    [DataField(required: true)]
    public List<string> AnchorIds = new();

    [DataField]
    public float Range = 1.5f;
}

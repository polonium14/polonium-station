namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Anchor is held in one of the hands.</summary>
public sealed partial class HoldingAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Player clicks the anchor — InteractHand or UseInHand.</summary>
public sealed partial class InteractAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

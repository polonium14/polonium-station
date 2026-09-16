namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Done when player is pulling the entity with given anchor (Ctrl+LMB).</summary>
public sealed partial class ItemPulledCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

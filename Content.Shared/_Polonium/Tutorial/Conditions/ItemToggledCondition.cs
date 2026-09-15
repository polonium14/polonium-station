namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class ItemToggledCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public bool Activated = true;
}

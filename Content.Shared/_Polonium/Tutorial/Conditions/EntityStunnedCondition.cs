namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class EntityStunnedCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class WearingSlotCondition : TutorialCondition
{
    [DataField(required: true)]
    public string Slot = string.Empty;
}

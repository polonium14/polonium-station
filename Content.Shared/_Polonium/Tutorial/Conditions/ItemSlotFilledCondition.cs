namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Something sits in the anchor's container, e.g. a magazine in a gun's gun_magazine slot.</summary>
public sealed partial class ItemSlotFilledCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField(required: true)]
    public string Slot = string.Empty;
}

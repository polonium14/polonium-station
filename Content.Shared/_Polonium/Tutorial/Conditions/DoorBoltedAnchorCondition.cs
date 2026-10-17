namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>State of the bolts on the door behind an anchor. Defaults to "the bolts are gone".</summary>
public sealed partial class DoorBoltedAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public bool Bolted;
}

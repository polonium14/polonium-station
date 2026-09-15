using Content.Shared.Doors.Components;

namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class DoorStateAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public DoorState State = DoorState.Open;
}

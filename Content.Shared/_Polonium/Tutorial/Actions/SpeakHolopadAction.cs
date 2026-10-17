namespace Content.Shared._Polonium.Tutorial.Actions;

public sealed partial class SpeakHolopadAction : TutorialAction
{
    [DataField(required: true)]
    public List<LocId> Lines = new();
}

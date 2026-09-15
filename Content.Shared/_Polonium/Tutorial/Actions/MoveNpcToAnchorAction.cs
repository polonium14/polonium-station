namespace Content.Shared._Polonium.Tutorial.Actions;

public sealed partial class MoveNpcToAnchorAction : TutorialAction
{
    [DataField(required: true)]
    public string NpcAnchorId = string.Empty;

    [DataField(required: true)]
    public string TargetAnchorId = string.Empty;

    /// <summary>Let him walk there on his own legs instead of blinking across the room.</summary>
    [DataField]
    public bool Walk;
}

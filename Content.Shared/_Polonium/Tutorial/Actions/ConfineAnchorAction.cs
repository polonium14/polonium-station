namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// Keeps the anchor inside its room. Dragged into one of the doorways, it is let go and ends up
/// back where it was, whether it is pulled on its own or inside something like a body bag.
/// </summary>
public sealed partial class ConfineAnchorAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    /// <summary>Anchors of the doors out of the room.</summary>
    [DataField(required: true)]
    public List<string> Doorways = new();

    /// <summary>Shown to whoever tried to drag it out.</summary>
    [DataField]
    public LocId? Popup;
}

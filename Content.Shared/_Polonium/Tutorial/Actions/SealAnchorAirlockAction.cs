namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// Shuts a door for good - closed, bolted and with the wire panel welded shut in spirit.
/// Unlike UnlockAnchorAirlockAction this one is not meant to be undone, it is the point of no return.
/// </summary>
public sealed partial class SealAnchorAirlockAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

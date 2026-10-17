namespace Content.Shared._Polonium.Tutorial.Watchers;

public sealed partial class SlipTeleportWatcher : TutorialWatcher
{
    [DataField(required: true)]
    public string TeleportAnchor = string.Empty;
}

namespace Content.Shared._Polonium.Tutorial.Watchers;

public sealed partial class AnchorEmptyOrGoneWatcher : TutorialWatcher
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

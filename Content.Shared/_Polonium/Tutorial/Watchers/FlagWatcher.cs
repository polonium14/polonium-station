namespace Content.Shared._Polonium.Tutorial.Watchers;

public sealed partial class FlagWatcher : TutorialWatcher
{
    [DataField(required: true)]
    public string Flag = string.Empty;
}

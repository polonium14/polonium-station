namespace Content.Shared._Polonium.Tutorial.Watchers;

/// <summary>Fires the moment the trainee picks the anchor up.</summary>
public sealed partial class HoldingAnchorWatcher : TutorialWatcher
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

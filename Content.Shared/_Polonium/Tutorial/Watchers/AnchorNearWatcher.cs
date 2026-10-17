namespace Content.Shared._Polonium.Tutorial.Watchers;

public sealed partial class AnchorNearWatcher : TutorialWatcher
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField(required: true)]
    public string NearAnchorId = string.Empty;

    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Flip the test: fire once the two anchors are further apart than <see cref="Range"/>.
    /// Use it to notice that something left the spot it started on. Both anchors still have to
    /// exist, so an object that was destroyed rather than moved does not count.
    /// </summary>
    [DataField]
    public bool Away;
}

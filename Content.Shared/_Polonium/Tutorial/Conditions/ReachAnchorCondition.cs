namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Player gets close enough to the anchor. Polled every 250ms.</summary>
public sealed partial class ReachAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    /// <summary>Metres. Default is arm's length-ish.</summary>
    [DataField]
    public float Range = 1.5f;
}

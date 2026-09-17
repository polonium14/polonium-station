namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// The mentor reads out how the shooting drill of this step went: hits, misses and a verdict.
/// Stays quiet if nothing was fired.
/// </summary>
public sealed partial class ShotScoreAction : TutorialAction
{
    [DataField(required: true)]
    public LocId Summary;

    /// <summary>The first entry the accuracy reaches wins, so list them best first.</summary>
    [DataField]
    public List<TutorialShotVerdict> Verdicts = new();
}

[DataDefinition]
public sealed partial class TutorialShotVerdict
{
    /// <summary>Hits divided by shots, 0 to 1.</summary>
    [DataField]
    public float MinAccuracy;

    [DataField(required: true)]
    public LocId Line;
}

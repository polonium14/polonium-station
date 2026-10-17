namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Every anchor with this id got shot at, one after another, nearest to OrderFrom first.
/// Only the aim counts, a miss moves the drill along just the same.
/// </summary>
public sealed partial class ShootTargetsCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    /// <summary>Targets go nearest to this anchor first. Null means nearest to the trainee.</summary>
    [DataField]
    public string? OrderFrom;

    [DataField]
    public int ShotsEach = 1;

    /// <summary>How far from a target the click may land and still count as aimed at it.</summary>
    [DataField]
    public float AimRange = 1f;
}

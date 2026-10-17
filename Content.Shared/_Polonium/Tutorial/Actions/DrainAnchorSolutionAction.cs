namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// Tips the excess out of a container the trainee overfilled, leaving <see cref="Keep"/> units of
/// every reagent behind. A hydroponics tray that has been flooded to the brim cannot take the
/// fertiliser the step is waiting for, and before anything is planted nothing drains on its own.
/// </summary>
public sealed partial class DrainAnchorSolutionAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    /// <summary>Solution name. Null drains every solution on the entity.</summary>
    [DataField]
    public string? Solution;

    /// <summary>Units of each reagent to leave in place.</summary>
    [DataField]
    public float Keep;
}

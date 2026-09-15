namespace Content.Shared._Polonium.Tutorial.Actions;

public sealed partial class OpenStorageAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    /// <summary>
    /// Skipped on step entry, the trainee opens it with their own access. Only the stuck
    /// recovery pops it open.
    /// </summary>
    [DataField]
    public bool RecoveryOnly;
}

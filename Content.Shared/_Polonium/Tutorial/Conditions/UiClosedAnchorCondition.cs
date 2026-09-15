namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// The anchor's window is shut again. This reads the live UI state rather than waiting for a close
/// event: the step that opened the window advances the moment it appears, so a trainee who closes
/// it during the settle delay would otherwise be waiting on an event that has already gone past.
/// </summary>
public sealed partial class UiClosedAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

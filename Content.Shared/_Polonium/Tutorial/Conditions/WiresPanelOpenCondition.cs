namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// The maintenance panel of a machine or airlock is unscrewed. Half of hacking is getting the
/// cover off, and that half deserves to be asked for separately.
/// </summary>
public sealed partial class WiresPanelOpenCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public bool Open = true;
}

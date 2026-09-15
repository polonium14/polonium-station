namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// Switches every light on the anchor on or off. Room 9 has no light switch on the map, so the
/// facility cuts the lights itself at the dramatically convenient moment.
/// </summary>
public sealed partial class SetLightsAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public bool On;
}

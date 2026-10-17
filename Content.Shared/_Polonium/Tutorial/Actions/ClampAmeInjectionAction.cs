namespace Content.Shared._Polonium.Tutorial.Actions;

public sealed partial class ClampAmeInjectionAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    /// <summary>Also drop the current setting to the limit. Off just puts the limit in place.</summary>
    [DataField]
    public bool Clamp = true;
}

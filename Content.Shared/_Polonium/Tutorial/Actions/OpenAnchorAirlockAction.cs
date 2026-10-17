namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>Lifts the bolts and swings the door open, whatever its power or access say.</summary>
public sealed partial class OpenAnchorAirlockAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

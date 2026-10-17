namespace Content.Shared._Polonium.Tutorial.Actions;

public sealed partial class UnlockAnchorAirlockAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

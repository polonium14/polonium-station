namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>Sets a step flag. Flags are wiped when the next step starts.</summary>
public sealed partial class SetFlagAction : TutorialAction
{
    [DataField(required: true)]
    public string Flag = string.Empty;
}

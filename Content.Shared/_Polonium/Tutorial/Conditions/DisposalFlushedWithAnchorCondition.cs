namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class DisposalFlushedWithAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string DisposalAnchorId = string.Empty;

    [DataField]
    public string? ItemAnchorId;
}

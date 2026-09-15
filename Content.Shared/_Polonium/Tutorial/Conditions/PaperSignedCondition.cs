namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Somebody left a signature on the paper. Any signature, the name is not checked.</summary>
public sealed partial class PaperSignedCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}

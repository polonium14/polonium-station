namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class BuckledToAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    /// <summary>Who has to be sitting there. Null means the trainee.</summary>
    [DataField]
    public string? EntityAnchorId;
}

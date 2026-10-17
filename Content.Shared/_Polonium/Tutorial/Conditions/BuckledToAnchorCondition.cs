namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class BuckledToAnchorCondition : TutorialCondition
{
    /// <summary>What they sit or lie on. Null means anything a body lies down on, any bed will do.</summary>
    [DataField]
    public string? AnchorId;

    /// <summary>Who has to be sitting there. Null means the trainee.</summary>
    [DataField]
    public string? EntityAnchorId;
}

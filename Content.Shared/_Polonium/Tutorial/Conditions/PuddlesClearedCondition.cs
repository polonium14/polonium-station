namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>No puddles left. Scope it to an anchor, otherwise a spill in an earlier room blocks the step.</summary>
public sealed partial class PuddlesClearedCondition : TutorialCondition
{
    [DataField]
    public string? NearAnchorId;

    [DataField]
    public float Range = 8f;
}

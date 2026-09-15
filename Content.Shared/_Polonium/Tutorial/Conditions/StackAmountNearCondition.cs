using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// At least this much of a stack type on the trainee and around them (or around an anchor), whatever the
/// stacks happen to be split into. Thirty sheets in one pile and three piles of ten read the same.
/// </summary>
public sealed partial class StackAmountNearCondition : TutorialCondition
{
    [DataField(required: true)]
    public ProtoId<StackPrototype> StackType;

    [DataField]
    public int Min = 1;

    /// <summary>Where to look besides the trainee's pockets. Unset means around the trainee.</summary>
    [DataField]
    public string? AnchorId;

    [DataField]
    public float Range = 6f;
}

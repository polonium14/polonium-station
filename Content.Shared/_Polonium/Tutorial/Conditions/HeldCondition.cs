namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// The inner condition has been true without a break for this long. Only counts while something
/// evaluates it, so inside All/Any the clock starts once the earlier entries let it through.
/// </summary>
public sealed partial class HeldCondition : TutorialCondition
{
    [DataField(required: true)]
    public TutorialCondition Condition = default!;

    [DataField(required: true)]
    public float Seconds;
}

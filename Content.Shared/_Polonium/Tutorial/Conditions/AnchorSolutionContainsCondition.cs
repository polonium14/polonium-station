using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// A reagent sitting in an anchor's own solution - the soil of a hydroponics tray, the tank of a
/// sink. Unlike the drainable check this reads the container itself, not what is lying near it.
/// </summary>
public sealed partial class AnchorSolutionContainsCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField]
    public float Amount = 1f;

    /// <summary>Which solution to read. Null adds up every solution on the entity.</summary>
    [DataField]
    public string? Solution;
}

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Enough of a reagent sitting in containers that will actually give it up. Drainable is the same
/// test a microwave runs, so a whole egg holding six units of egg does not count until someone
/// cracks it into something.
/// </summary>
public sealed partial class DrainableReagentNearbyCondition : TutorialCondition
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField]
    public float Amount = 1f;

    [DataField]
    public float Range = 4f;

    /// <summary>Search around this anchor instead of around the trainee.</summary>
    [DataField]
    public string? AnchorId;

    /// <summary>Count only what is inside the anchor, not what is standing next to it.</summary>
    [DataField]
    public bool Inside;
}

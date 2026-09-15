using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// What one named kind of container is holding, wherever the trainee left it. The nearby-reagent
/// check cannot do this job in a kitchen: the sink standing next to them carries four hundred
/// units of water and would answer yes to every question about water.
/// </summary>
public sealed partial class PrototypeSolutionContainsCondition : TutorialCondition
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField]
    public float Amount = 1f;

    [DataField]
    public float Range = 6f;

    /// <summary>Which solution to read. Null adds up every solution on the container.</summary>
    [DataField]
    public string? Solution;
}

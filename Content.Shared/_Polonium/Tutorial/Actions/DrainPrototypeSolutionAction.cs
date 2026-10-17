using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// Tips the excess out of a container the trainee overfilled, leaving <see cref="Keep"/> units of
/// every reagent behind. Same idea as the anchor version, for things that came out of a vending
/// machine and so never had an anchor to begin with.
/// </summary>
public sealed partial class DrainPrototypeSolutionAction : TutorialAction
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    /// <summary>Solution name. Null drains every solution on the container.</summary>
    [DataField]
    public string? Solution;

    /// <summary>Units of each reagent to leave in place.</summary>
    [DataField]
    public float Keep;

    /// <summary>Only touch this reagent. Null drains everything the container holds.</summary>
    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    [DataField]
    public float Range = 6f;
}

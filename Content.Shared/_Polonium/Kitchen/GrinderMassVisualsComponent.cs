using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Kitchen;

/// <summary>
/// Tells the window of a reagent grinder when the ground mass is drawn as dry dust and not as a wet paste.
/// </summary>
[RegisterComponent, Access(Other = AccessPermissions.ReadExecute)]
public sealed partial class GrinderMassVisualsComponent : Component
{
    /// <summary>
    /// Reagents that come out of the grinder dry although they have no melting point of their own, such as flour.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> DryReagents = new();

    /// <summary>
    /// A reagent that melts above this temperature counts as dry. It is in the unit the reagent prototypes use for melting points.
    /// </summary>
    [DataField]
    public float DryMeltingPoint = 20f;

    /// <summary>
    /// The mass is dust when at least this share of its volume is dry.
    /// </summary>
    [DataField]
    public float DustShare = 0.5f;
}

using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// The abductor event in the rotation: starts a lone abductor or a pair depending on the online.
/// </summary>
[RegisterComponent, Access(typeof(AbductorDispatchRuleSystem))]
public sealed partial class AbductorDispatchRuleComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Lone;

    [DataField(required: true)]
    public EntProtoId Pair;

    [DataField]
    public int PairMinimumPlayers = 20;
}

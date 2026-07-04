using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(SecretRuleSystem))]
public sealed partial class SecretRuleComponent : Component
{
    /// <summary>
    /// The gamerules that get added by secret.
    /// </summary>
    [DataField("additionalGameRules")]
    public HashSet<EntityUid> AdditionalGameRules = new();

    /// <summary>
    /// Weight table for preset selection. Falls back to <see cref="Content.Shared.CCVar.CCVars.SecretWeightPrototype"/> when unset.
    /// </summary>
    [DataField]
    public ProtoId<WeightedRandomPrototype>? WeightTable;
}

using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Replicator;

[Prototype]
public sealed partial class ReplicatorStructurePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Entity;

    /// <summary>
    /// Price in whole sheets of replisteel, taken from the stack the builder holds.
    /// </summary>
    [DataField(required: true)]
    public int Cost;

    /// <summary>
    /// The price is multiplied by this for every structure of the kind that already stands.
    /// </summary>
    [DataField]
    public float CostGrowth = 1f;

    [DataField]
    public int MinNestLevel = 1;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(10);

    [DataField]
    public float Range = 2f;

    [DataField]
    public EntProtoId? Effect = "EffectReplicatorBuild";

    [DataField]
    public int Order;
}

using Content.Shared.Materials;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Replicator;

/// <summary>
/// The hive budget is kept as material in the nest's <see cref="MaterialStorageComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class ReplicatorHiveComponent : Component
{
    [DataField]
    public ProtoId<MaterialPrototype> Material = "Replisteel";

    [DataField]
    public int UnitsPerPoint = 20;

    [DataField]
    public int DeadReplicatorPoints = 5;

    [DataField]
    public float SpillFraction = 0.5f;

    [DataField]
    public List<ReplicatorRebuildOption> Rebuilds = new();

    [DataField]
    public TimeSpan RebuildTime = TimeSpan.FromSeconds(12);

    [DataField]
    public int MinReplicators = 3;

    [DataField]
    public int PlayersPerReplicator = 6;

    [DataField]
    public int MaxReplicators = 12;
}

[DataDefinition]
public sealed partial class ReplicatorRebuildOption
{
    [DataField(required: true)]
    public int Tier;

    [DataField]
    public int Cost;

    [DataField]
    public int MinNestLevel = 1;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public SpriteSpecifier? Icon;
}

using Content.Shared.Damage;
using Content.Shared.Materials;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Replicator;

/// <summary>
/// The hive budget is kept as material in the nest's <see cref="MaterialStorageComponent"/>.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
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

    /// <summary>
    /// Sheets can be printed out of the nest only while this much material stays behind.
    /// </summary>
    [DataField]
    public int WithdrawReserve = 200;

    [DataField]
    public DamageSpecifier Heal = new();

    [DataField]
    public float HealRange = 2.5f;

    [DataField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextHeal;

    [DataField]
    public int MinReplicators = 3;

    [DataField]
    public int PlayersPerReplicator = 6;

    [DataField]
    public int MaxReplicators = 12;

    /// <summary>
    /// The nest's destruction threshold is multiplied by this on every level it gains, up to its endgame level.
    /// </summary>
    [DataField]
    public float HealthGrowth = 1.4f;

    [DataField]
    public TimeSpan QueenRespawnDelay = TimeSpan.FromMinutes(3);

    [DataField]
    public EntProtoId QueenSpawner = "SpawnPointGhostReplicatorQueen";

    [DataField]
    public TimeSpan QueenCheckInterval = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextQueenCheck;

    [ViewVariables]
    public TimeSpan? QueenDueAt;
}

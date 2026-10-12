using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Replicator;

[Prototype]
public sealed partial class ReplicatorModulePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public List<ReplicatorModuleStage> Stages = new();
}

[DataDefinition]
public sealed partial class ReplicatorModuleStage
{
    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public int Cost;

    [DataField]
    public int MinNestLevel = 1;

    [DataField]
    public float SpeedMultiplier = 1f;

    [DataField]
    public float FireRateMultiplier = 1f;

    /// <summary>
    /// Extra charges the stun ray holds.
    /// </summary>
    [DataField]
    public int GunCapacityBonus;

    /// <summary>
    /// Scales the time the stun ray needs to regain one charge.
    /// </summary>
    [DataField]
    public float GunRechargeMultiplier = 1f;

    [DataField]
    public float DamageCoefficient = 1f;

    [DataField]
    public float BuildTimeMultiplier = 1f;

    [DataField]
    public DamageSpecifier? MeleeBonus;

    [DataField]
    public float TeleportDelayMultiplier = 1f;

    [DataField]
    public TimeSpan TeleportParalyzeBonus;

    [DataField]
    public float TeleportImmunityMultiplier = 1f;

    [DataField]
    public float TowFrictionMultiplier = 1f;

    [DataField]
    public float TowAccelerationMultiplier = 1f;

    [DataField]
    public bool FloorSpread;

    [DataField]
    public ComponentRegistry Components = new();

    [DataField]
    public List<EntProtoId> Items = new();

    [DataField]
    public int SheetHands;

    [DataField]
    public List<EntProtoId> Actions = new();
}

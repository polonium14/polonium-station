using Content.Shared.FixedPoint;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Replicator;

[Prototype]
public sealed partial class ReplicatorClassPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public ResPath Sprite;

    [DataField(required: true)]
    public List<ReplicatorClassForm> Forms = new();

    [DataField(required: true)]
    public List<ProtoId<ReplicatorModulePrototype>> Modules = new();

    [DataField]
    public int Cost;

    [DataField]
    public int Order;

    [DataField]
    public float SpeedMultiplier = 1f;

    /// <summary>
    /// Added to the replicator once when its shell is rebuilt for this class.
    /// </summary>
    [DataField]
    public ComponentRegistry Components = new();

    [DataField]
    public ProtoId<StatusIconPrototype>? Icon;

    public ReplicatorClassForm? GetForm(int stages)
    {
        ReplicatorClassForm? result = null;
        foreach (var form in Forms)
        {
            if (result == null || stages >= form.Stages)
                result = form;
        }

        return result;
    }
}

[DataDefinition]
public sealed partial class ReplicatorClassForm
{
    [DataField(required: true)]
    public string State = string.Empty;

    [DataField]
    public int Stages;

    /// <summary>
    /// Damage at which a replicator in this form dies.
    /// </summary>
    [DataField]
    public FixedPoint2? Health;

    public string CombatState => $"{State}_combat";

    public string DeadState => $"{State}_dead";

    public string ThrustState => $"{State}_thrust";
}

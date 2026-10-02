using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Polonium.EntityTable;

/// <summary>
/// Table row for a station event that can change its price, weight and requirements just for this table.
/// Schedulers that don't know about the overrides see a plain entry.
/// </summary>
public sealed partial class EventSelector : EntityTableSelector
{
    /// <summary>
    /// Put a dictionary under this key in <see cref="EntityTableContext"/> and the rolled rows end up in it, by event id.
    /// </summary>
    public const string OverridesKey = "EventOverrides";

    [DataField(required: true)]
    public EntProtoId Id;

    [DataField]
    public float? ChaosScore;

    [DataField]
    public float? EventWeight;

    [DataField]
    public int? MinimumPlayers;

    [DataField]
    public int? EarliestStart;

    [DataField]
    public int? ReoccurrenceDelay;

    protected override IEnumerable<EntProtoId> GetSpawnsImplementation(IRobustRandom rand,
        IEntityManager entMan,
        IPrototypeManager proto,
        EntityTableContext ctx)
    {
        if (ctx.TryGetData<Dictionary<EntProtoId, EventSelector>>(OverridesKey, out var overrides))
            overrides.TryAdd(Id, this);

        yield return Id;
    }

    protected override IEnumerable<(EntProtoId spawn, double)> ListSpawnsImplementation(IEntityManager entMan, IPrototypeManager proto, EntityTableContext ctx)
    {
        yield return (Id, 1f);
    }

    protected override IEnumerable<(EntProtoId spawn, double)> AverageSpawnsImplementation(IEntityManager entMan, IPrototypeManager proto, EntityTableContext ctx)
    {
        yield return (Id, 1f);
    }
}

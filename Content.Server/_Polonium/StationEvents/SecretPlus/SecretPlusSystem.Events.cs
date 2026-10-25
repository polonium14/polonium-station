using Content.Server.StationEvents.Components;
using Content.Shared._Polonium.EntityTable;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.SecretPlus;

public sealed partial class SecretPlusSystem
{
    [Dependency] private EntityTableSystem _entityTable = default!;

    /// <summary>
    /// Rolls the table and keeps the events that can run right now. A row with overrides is checked against its own requirements.
    /// </summary>
    private void AddTableEvents(
        Entity<SecretPlusComponent> scheduler,
        EntityTableSelector table,
        Dictionary<EntityPrototype, StationEventComponent> available,
        int players,
        bool ghostAntag)
    {
        var overrides = new Dictionary<EntProtoId, EventSelector>();
        var ctx = new EntityTableContext();
        ctx.SetData(EventSelector.OverridesKey, overrides);
        var seen = new HashSet<EntityPrototype>();

        foreach (var id in _entityTable.GetSpawns(table, ctx: ctx))
        {
            if (!_prototypeManager.Resolve(id, out var proto)
                || proto.Abstract
                || !seen.Add(proto)
                || !proto.TryComp<StationEventComponent>(out var stationEvent, _factory)
                || !proto.TryComp<GameRuleComponent>(out var gameRule, _factory)
                || scheduler.Comp.DisallowedEvents.Contains(stationEvent.EventType))
                continue;

            var row = overrides.GetValueOrDefault(id);
            if (row == null ? !available.ContainsKey(proto) : !CanRunRow(scheduler, proto, stationEvent, row, players))
                continue;

            scheduler.Comp.SelectedEvents.Add(new SelectedEvent(proto, gameRule, stationEvent, ghostAntag, row?.ChaosScore, row?.EventWeight));
        }
    }

    private float GetEventSpeed(Entity<SecretPlusComponent> scheduler, PlayerCount count)
    {
        var speed = GetByPlayerCount(scheduler.Comp.EventSpeedByPlayers, GetEventPlayerCount(scheduler, count));
        return speed > 0f ? speed : 1f;
    }

    private bool CanRunRow(
        Entity<SecretPlusComponent> scheduler,
        EntityPrototype proto,
        StationEventComponent stationEvent,
        EventSelector row,
        int players)
    {
        var ignore = scheduler.Comp.IgnoreTimings;
        return _event.CanRun(proto,
            stationEvent,
            ignore ? int.MaxValue : players,
            ignore ? TimeSpan.MaxValue : _ticker.RoundDuration(),
            1f / GetRamping(scheduler),
            row.MinimumPlayers,
            row.EarliestStart,
            row.ReoccurrenceDelay);
    }
}

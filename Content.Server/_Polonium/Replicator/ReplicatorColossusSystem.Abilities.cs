using System.Numerics;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorColossusSystem
{
    private void InitializeAbilities()
    {
        SubscribeLocalEvent<ReplicatorColossusPulseComponent, ReplicatorColossusPulseActionEvent>(OnPulse);
        SubscribeLocalEvent<ReplicatorColossusPulseComponent, ReplicatorColossusPulseDoAfterEvent>(OnPulseDoAfter);
        SubscribeLocalEvent<ReplicatorColossusSweepComponent, ReplicatorColossusSweepActionEvent>(OnSweep);
    }

    private void UpdateAbilities(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ReplicatorColossusSweepComponent>();
        while (query.MoveNext(out var uid, out var sweep))
        {
            if (sweep.Pending.Count == 0)
                continue;

            foreach (var pending in sweep.Pending)
            {
                if (pending.Time <= now)
                    ConvertPending((uid, sweep), pending);
            }

            sweep.Pending.RemoveAll(pending => pending.Time <= now);
        }
    }

    #region Pulse

    private void OnPulse(Entity<ReplicatorColossusPulseComponent> ent, ref ReplicatorColossusPulseActionEvent args)
    {
        if (args.Handled || IsBlocked(ent))
            return;

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.Delay, new ReplicatorColossusPulseDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DamageThreshold = ent.Comp.DamageThreshold,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;

        if (ent.Comp.ChargeEffect is { } effect)
            ent.Comp.ChargeEffectEntity = SpawnAttachedTo(effect, new EntityCoordinates(ent, Vector2.Zero));

        _audio.PlayPvs(ent.Comp.ChargeSound, ent);
    }

    private void OnPulseDoAfter(Entity<ReplicatorColossusPulseComponent> ent, ref ReplicatorColossusPulseDoAfterEvent args)
    {
        if (ent.Comp.ChargeEffectEntity is { } charge)
        {
            QueueDel(charge);
            ent.Comp.ChargeEffectEntity = null;
        }

        if (args.Handled || args.Cancelled || IsDisarmed(ent) || !TryComp<ReplicatorComponent>(ent, out var replicator))
            return;

        args.Handled = true;
        _colossusGun.Drain(ent.Owner, ent.Comp.AmmoDrain);

        var xform = Transform(ent);
        if (ent.Comp.Effect is { } effect)
            Spawn(effect, xform.Coordinates);

        _audio.PlayPvs(ent.Comp.Sound, ent);

        _mobs.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Radius, _mobs);
        foreach (var mob in _mobs)
        {
            if (mob.Owner == ent.Owner
                || HasComp<ReplicatorComponent>(mob)
                || _mobState.IsDead(mob, mob.Comp)
                || _recallImmunity.IsWarded(mob))
                continue;

            if (TryComp<PullableComponent>(mob, out var pullable) && pullable.BeingPulled)
                _pulling.TryStopPull(mob, pullable);

            if (!_replicator.TryTeleportAway((ent.Owner, replicator), mob))
                continue;

            _stun.TryAddParalyzeDuration(mob, replicator.TeleportPreyParalyzeDuration, visualized: true);
            _recallImmunity.Apply(mob, replicator.TeleportPreyImmunityDuration);
            _audio.PlayPvs(replicator.TeleportPreySound, mob);

            if (ent.Comp.ArrivalEffect is { } arrival)
                Spawn(arrival, Transform(mob).Coordinates);

            _popup.PopupEntity(Loc.GetString("replicator-colossus-pulse-target"), mob, mob, PopupType.Medium);
        }
    }

    #endregion

    #region Sweep

    private void OnSweep(Entity<ReplicatorColossusSweepComponent> ent, ref ReplicatorColossusSweepActionEvent args)
    {
        if (args.Handled || IsBlocked(ent))
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("replicator-construction-fail-no-floor"), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        _colossusGun.Drain(ent.Owner, ent.Comp.AmmoDrain);

        var now = _timing.CurTime;
        var origin = _transform.GetWorldPosition(xform);
        var startAngle = _transform.GetWorldRotation(xform);
        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var reach = (int) MathF.Ceiling(ent.Comp.Radius);

        for (var x = -reach; x <= reach; x++)
        {
            for (var y = -reach; y <= reach; y++)
            {
                var indices = center + new Vector2i(x, y);
                var offset = _map.GridTileToWorldPos(gridUid, grid, indices) - origin;
                if (offset.Length() > ent.Comp.Radius)
                    continue;

                var turn = offset.LengthSquared() < 0.01f
                    ? 0.0
                    : (offset.ToWorldAngle() - startAngle).Reduced().FlipPositive().Theta / MathHelper.TwoPi;

                ent.Comp.Pending.Add(new ReplicatorColossusPendingConversion(now + ent.Comp.Duration * turn, gridUid, indices));
            }
        }

        if (ent.Comp.Effect is { } effect)
        {
            var beam = Spawn(effect, xform.Coordinates);
            var sweepEffect = EnsureComp<ReplicatorColossusSweepEffectComponent>(beam);
            sweepEffect.StartTime = now;
            sweepEffect.Duration = ent.Comp.Duration;
            sweepEffect.StartAngle = startAngle;
            Dirty(beam, sweepEffect);
        }

        _audio.PlayPvs(ent.Comp.Sound, ent);
    }

    private void ConvertPending(Entity<ReplicatorColossusSweepComponent> ent, ReplicatorColossusPendingConversion pending)
    {
        if (TerminatingOrDeleted(pending.Grid)
            || !TryComp<MapGridComponent>(pending.Grid, out var grid)
            || !ConvertToHive((pending.Grid, grid), pending.Indices, ent.Comp.Wall, ent.Comp.ReinforcedWall,
                ent.Comp.ReinforcedThreshold, ent.Comp.Tile, out var location))
            return;

        if (ent.Comp.ConvertEffect is { } effect)
            Spawn(effect, location);

        var now = _timing.CurTime;
        if (now < ent.Comp.NextConvertSound)
            return;

        ent.Comp.NextConvertSound = now + ent.Comp.ConvertSoundInterval;
        _audio.PlayPvs(ent.Comp.ConvertSound, location);
    }

    #endregion

    private bool IsBlocked(EntityUid uid)
    {
        if (!IsDisarmed(uid))
            return false;

        _popup.PopupEntity(Loc.GetString("replicator-colossus-disarmed"), uid, uid, PopupType.SmallCaution);
        return true;
    }

    private bool ConvertToHive(Entity<MapGridComponent> grid, Vector2i indices, EntProtoId wallProto, EntProtoId reinforcedWallProto,
        float reinforcedThreshold, ProtoId<ContentTileDefinition> tileProto, out EntityCoordinates location)
    {
        location = default;

        if (!_map.TryGetTileRef(grid, grid, indices, out var tile) || _turf.IsSpace(tile))
            return false;

        location = _map.GridTileToLocal(grid, grid, indices);
        var converted = false;

        if (_construction.FindWall(grid, indices) is { } wall && _construction.CanConvertWall(wall, out _))
        {
            var replacement = _destructible.DestroyedAt(wall).Float() >= reinforcedThreshold ? reinforcedWallProto : wallProto;

            QueueDel(wall);
            Spawn(replacement, location);
            converted = true;
        }

        var floor = (ContentTileDefinition) _tileDefinitions[tileProto];
        if (tile.Tile.TypeId != floor.TileId)
        {
            _tile.ReplaceTile(tile, floor);
            converted = true;
        }

        return converted;
    }
}

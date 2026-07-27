using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Construction.FloorResin;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Weeds;

public abstract partial class SharedXenoWeedsSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _toUpdate = new();
    private readonly HashSet<Entity<AffectableByWeedsComponent>> _intersecting = new();

    private EntityQuery<AffectableByWeedsComponent> _affectedQuery;
    private EntityQuery<XenoComponent> _xenoQuery;
    private EntityQuery<BlockWeedsComponent> _blockWeedsQuery;

    public override void Initialize()
    {
        _affectedQuery = GetEntityQuery<AffectableByWeedsComponent>();
        _xenoQuery = GetEntityQuery<XenoComponent>();
        _blockWeedsQuery = GetEntityQuery<BlockWeedsComponent>();

        SubscribeLocalEvent<XenoWeedsComponent, MapInitEvent>(OnWeedsMapInit);
        SubscribeLocalEvent<XenoWeedsComponent, AnchorStateChangedEvent>(OnWeedsAnchorChanged);
        SubscribeLocalEvent<XenoWeedsComponent, StartCollideEvent>(OnWeedsStartCollide);
        SubscribeLocalEvent<XenoWeedsComponent, EndCollideEvent>(OnWeedsEndCollide);

        SubscribeLocalEvent<XenoStickyResinComponent, StartCollideEvent>(OnStickyStartCollide);
        SubscribeLocalEvent<XenoStickyResinComponent, EndCollideEvent>(OnStickyEndCollide);

        SubscribeLocalEvent<AffectableByWeedsComponent, MoveEvent>(OnAffectedMove);
        SubscribeLocalEvent<AffectableByWeedsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<AffectableByWeedsComponent, ComponentStartup>(OnAffectedStartup);

        SubscribeLocalEvent<DamageOffWeedsComponent, MapInitEvent>(OnDamageOffWeedsMapInit);
    }

    private void OnWeedsMapInit(Entity<XenoWeedsComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Spreads)
        {
            var spreading = EnsureComp<XenoWeedsSpreadingComponent>(ent);
            spreading.NextSpread = _timing.CurTime + spreading.SpreadDelay;
            Dirty(ent, spreading);
        }

        _intersecting.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 0.5f, _intersecting);
        foreach (var intersecting in _intersecting)
            _toUpdate.Add(intersecting);
    }

    private void OnWeedsAnchorChanged(Entity<XenoWeedsComponent> weeds, ref AnchorStateChangedEvent args)
    {
        if (_net.IsServer && !args.Anchored)
            QueueDel(weeds);
    }

    private void OnWeedsStartCollide(Entity<XenoWeedsComponent> ent, ref StartCollideEvent args)
    {
        if (_affectedQuery.HasComp(args.OtherEntity))
            _toUpdate.Add(args.OtherEntity);
    }

    private void OnWeedsEndCollide(Entity<XenoWeedsComponent> ent, ref EndCollideEvent args)
    {
        if (_affectedQuery.HasComp(args.OtherEntity))
            _toUpdate.Add(args.OtherEntity);
    }

    private void OnStickyStartCollide(Entity<XenoStickyResinComponent> ent, ref StartCollideEvent args)
    {
        if (_affectedQuery.HasComp(args.OtherEntity))
            _toUpdate.Add(args.OtherEntity);
    }

    private void OnStickyEndCollide(Entity<XenoStickyResinComponent> ent, ref EndCollideEvent args)
    {
        if (_affectedQuery.HasComp(args.OtherEntity))
            _toUpdate.Add(args.OtherEntity);
    }

    private void OnAffectedMove(Entity<AffectableByWeedsComponent> ent, ref MoveEvent args)
    {
        if (!args.OnlyRotation)
            _toUpdate.Add(ent);
    }

    private void OnAffectedStartup(Entity<AffectableByWeedsComponent> ent, ref ComponentStartup args)
    {
        _toUpdate.Add(ent);
    }

    private void OnRefreshSpeed(Entity<AffectableByWeedsComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.OnStickyResin && !_xenoQuery.HasComp(ent))
        {
            args.ModifySpeed(0.4f, 0.4f);
            return;
        }

        if (!ent.Comp.OnWeeds)
            return;

        if (_xenoQuery.HasComp(ent) || HasComp<XenoFriendlyComponent>(ent))
            return;

        args.ModifySpeed(0.5714f, 0.5714f);
    }

    private void OnDamageOffWeedsMapInit(Entity<DamageOffWeedsComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.DamageAt = _timing.CurTime + ent.Comp.Every;
        Dirty(ent);
    }

    public bool IsOnWeeds(EntityCoordinates coordinates)
    {
        return _rmcMap.HasAnchoredEntityEnumerator<XenoWeedsComponent>(coordinates);
    }

    public bool IsOnWeeds(EntityUid entity)
    {
        return IsOnWeeds(Transform(entity).Coordinates);
    }

    public bool IsOnFriendlyWeeds(EntityUid entity)
    {
        return IsOnFriendlyWeeds((entity, CompOrNull<AffectableByWeedsComponent>(entity)));
    }

    public bool IsOnFriendlyWeeds(Entity<AffectableByWeedsComponent?> ent)
    {
        if (_affectedQuery.TryComp(ent, out var affected) && affected.OnFriendlyWeeds)
            return true;

        var coords = Transform(ent).Coordinates;
        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator<XenoWeedsComponent>(coords);
        while (anchored.MoveNext(out var weeds))
        {
            if (_hive.FromSameHive(ent.Owner, weeds))
                return true;
        }

        return false;
    }

    public EntityUid? GetWeedsOnFloor(EntityCoordinates coordinates)
    {
        if (_rmcMap.HasAnchoredEntityEnumerator<XenoWeedsComponent>(coordinates, out var weeds))
            return weeds;

        return null;
    }

    public bool CanSpreadOnto(EntityCoordinates coordinates)
    {
        if (IsOnWeeds(coordinates))
            return false;

        if (_rmcMap.IsTileBlocked(coordinates))
            return false;

        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(coordinates);
        while (anchored.MoveNext(out var uid))
        {
            if (_blockWeedsQuery.HasComp(uid))
                return false;
        }

        return true;
    }

    protected void UpdateAffected(EntityUid uid, AffectableByWeedsComponent? affected = null)
    {
        if (!_affectedQuery.Resolve(uid, ref affected, false))
            return;

        var coords = Transform(uid).Coordinates;
        var onWeeds = IsOnWeeds(coords);
        var onFriendly = false;
        if (onWeeds)
        {
            var anchored = _rmcMap.GetAnchoredEntitiesEnumerator<XenoWeedsComponent>(coords);
            while (anchored.MoveNext(out var weeds))
            {
                if (_hive.FromSameHive(uid, weeds))
                {
                    onFriendly = true;
                    break;
                }
            }
        }

        var onSticky = _rmcMap.HasAnchoredEntityEnumerator<XenoStickyResinComponent>(coords);

        if (affected.OnWeeds == onWeeds &&
            affected.OnFriendlyWeeds == onFriendly &&
            affected.OnStickyResin == onSticky)
            return;

        affected.OnWeeds = onWeeds;
        affected.OnFriendlyWeeds = onFriendly;
        affected.OnStickyResin = onSticky;
        Dirty(uid, affected);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    public override void Update(float frameTime)
    {
        foreach (var uid in _toUpdate)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            UpdateAffected(uid);
        }

        _toUpdate.Clear();

        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var damageQuery = EntityQueryEnumerator<DamageOffWeedsComponent, AffectableByWeedsComponent>();
        while (damageQuery.MoveNext(out var uid, out var damage, out var affected))
        {
            if (damage.Skip || affected.OnWeeds)
            {
                damage.DamageAt = null;
                continue;
            }

            if (damage.RestingStopsDamage && HasComp<XenoRestingComponent>(uid))
                continue;

            damage.DamageAt ??= time + damage.Every;
            if (time < damage.DamageAt)
                continue;

            damage.DamageAt = time + damage.Every;
            _damageable.TryChangeDamage(uid, damage.Damage);
            Dirty(uid, damage);
        }
    }
}

using System.Numerics;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameStates;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorModuleAbilitySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ReplicatorHiveSystem _hive = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ReplicatorModuleSystem _modules = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _cargo = new();
    private readonly HashSet<Entity<HumanoidProfileComponent>> _crew = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorModulesComponent, ReplicatorVolleyActionEvent>(OnVolley);
        SubscribeLocalEvent<ReplicatorModulesComponent, ReplicatorRepairActionEvent>(OnRepair);
        SubscribeLocalEvent<ReplicatorModulesComponent, ReplicatorRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<ReplicatorModulesComponent, ReplicatorSendCargoActionEvent>(OnSendCargo);
        SubscribeLocalEvent<ReplicatorModulesComponent, ReplicatorMarkActionEvent>(OnMark);
        SubscribeLocalEvent<ReplicatorModulesComponent, ReplicatorScanActionEvent>(OnScan);

        SubscribeLocalEvent<ReplicatorMarkedComponent, ComponentShutdown>(OnMarkShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var marks = EntityQueryEnumerator<ReplicatorMarkedComponent>();
        while (marks.MoveNext(out var uid, out var marked))
        {
            if (now >= marked.EndTime)
                RemCompDeferred<ReplicatorMarkedComponent>(uid);
        }

        var trackers = EntityQueryEnumerator<ReplicatorTrackerComponent, TransformComponent>();
        while (trackers.MoveNext(out var uid, out var tracker, out var xform))
        {
            if (now < tracker.NextUpdate)
                continue;

            tracker.NextUpdate = now + tracker.Interval;

            var target = FindNearestCrew(xform);
            if (target == tracker.Target)
                continue;

            tracker.Target = target;
            Dirty(uid, tracker);
        }
    }

    private MapCoordinates FindNearestCrew(TransformComponent origin)
    {
        var nearest = MapCoordinates.Nullspace;
        var nearestDistance = float.MaxValue;
        var position = _transform.GetWorldPosition(origin);

        var crew = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent, TransformComponent>();
        while (crew.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (xform.MapID != origin.MapID || !_mobState.IsAlive(uid, mobState))
                continue;

            var crewPosition = _transform.GetWorldPosition(xform);
            var distance = Vector2.DistanceSquared(crewPosition, position);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = new MapCoordinates(crewPosition, xform.MapID);
        }

        return nearest;
    }

    #region Emitter

    private void OnVolley(Entity<ReplicatorModulesComponent> ent, ref ReplicatorVolleyActionEvent args)
    {
        if (args.Handled || args.Count <= 0)
            return;

        var origin = _transform.GetMapCoordinates(ent);
        var target = _transform.ToMapCoordinates(args.Target);
        var direction = target.Position - origin.Position;
        if (origin.MapId != target.MapId || direction.LengthSquared() < 0.01f)
            return;

        var centre = direction.ToAngle().Theta;
        for (var i = 0; i < args.Count; i++)
        {
            var offset = args.Count == 1 ? 0 : args.Spread.Theta * (i / (double) (args.Count - 1) - 0.5);
            var projectile = Spawn(args.Projectile, origin);
            _gun.ShootProjectile(projectile, new Angle(centre + offset).ToVec(), Vector2.Zero, ent, ent, args.Speed);
        }

        _audio.PlayPvs(args.Sound, ent);
        _modules.Reveal(ent);
        args.Handled = true;
    }

    #endregion

    #region Regeneration

    private void OnRepair(Entity<ReplicatorModulesComponent> ent, ref ReplicatorRepairActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (GetRepairBlocker(ent, target) is { } blocker)
        {
            _popup.PopupEntity(Loc.GetString(blocker), ent, ent, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, args.Delay, new ReplicatorRepairDoAfterEvent(args.Heal), ent, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = args.Range,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
        StopRepair(ent);

        var repairing = EnsureComp<ReplicatorRepairingComponent>(ent);
        if (args.Effect is { } effect)
            repairing.Effect = SpawnAttachedTo(effect, new EntityCoordinates(target, Vector2.Zero));

        if (args.Beam is { } beam)
        {
            var joint = EnsureComp<JointVisualsComponent>(ent);
            joint.Sprite = beam;
            joint.Target = target;
            Dirty(ent, joint);
        }

        _audio.PlayPvs(args.Sound, target);
    }

    private void OnRepairDoAfter(Entity<ReplicatorModulesComponent> ent, ref ReplicatorRepairDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!args.Cancelled && args.Target is { } target && GetRepairBlocker(ent, target) == null)
        {
            _damageable.HealEvenly(target, -args.Heal, origin: ent);
            if (IsDamaged(target))
            {
                args.Repeat = true;
                return;
            }
        }

        StopRepair(ent);
    }

    private string? GetRepairBlocker(EntityUid user, EntityUid target)
    {
        if (target == user)
            return "replicator-repair-fail-self";

        var swarmer = HasComp<ReplicatorComponent>(target) && !HasComp<ReplicatorColossusComponent>(target) && _mobState.IsAlive(target);
        if (!swarmer && !_tag.HasTag(target, ReplicatorHiveSystem.StructureTag))
            return "replicator-repair-fail-target";

        return IsDamaged(target) ? null : "replicator-repair-fail-intact";
    }

    private bool IsDamaged(EntityUid uid)
    {
        return TryComp<DamageableComponent>(uid, out var damageable) && !_damageable.GetPositiveDamage((uid, damageable)).Empty;
    }

    private void StopRepair(EntityUid uid)
    {
        if (!TryComp<ReplicatorRepairingComponent>(uid, out var repairing))
            return;

        if (repairing.Effect is { } effect)
            QueueDel(effect);

        RemComp<JointVisualsComponent>(uid);
        RemComp<ReplicatorRepairingComponent>(uid);
    }

    #endregion

    #region Cargo

    private void OnSendCargo(Entity<ReplicatorModulesComponent> ent, ref ReplicatorSendCargoActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_hive.TryGetHive(ent, out var hive))
        {
            _popup.PopupEntity(Loc.GetString("replicator-construction-fail-no-nest"), ent, ent, PopupType.SmallCaution);
            return;
        }

        _cargo.Clear();
        _cargo.AddRange(_hands.EnumerateHeld(ent.Owner));

        var sent = 0;
        foreach (var held in _cargo)
        {
            if (HasComp<UnremoveableComponent>(held))
                continue;

            var budget = _hive.GetStoredBudget(hive, held);
            if (budget <= 0)
                continue;

            sent += budget;
            QueueDel(held);
        }

        if (sent == 0)
        {
            _popup.PopupEntity(Loc.GetString("replicator-cargo-fail-empty"), ent, ent, PopupType.SmallCaution);
            return;
        }

        _hive.AddBudget(hive, sent);

        if (args.Effect is { } effect)
            Spawn(effect, Transform(ent).Coordinates);

        _audio.PlayPvs(args.Sound, ent);
        _popup.PopupEntity(Loc.GetString("replicator-cargo-sent", ("amount", ReplicatorHiveSystem.ToSheets(sent))), ent, ent);
        args.Handled = true;
    }

    #endregion

    #region Sensors

    private void OnMark(Entity<ReplicatorModulesComponent> ent, ref ReplicatorMarkActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<ReplicatorComponent>(args.Target) || !_mobState.IsAlive(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("replicator-mark-fail-target"), ent, ent, PopupType.SmallCaution);
            return;
        }

        Mark(args.Target, args.Duration);
        _audio.PlayEntity(args.Sound, ent, ent);
        _popup.PopupEntity(Loc.GetString("replicator-mark-applied", ("seconds", (int) args.Duration.TotalSeconds)), ent, ent);
        args.Handled = true;
    }

    private void OnScan(Entity<ReplicatorModulesComponent> ent, ref ReplicatorScanActionEvent args)
    {
        if (args.Handled)
            return;

        var coordinates = Transform(ent).Coordinates;
        _crew.Clear();
        _lookup.GetEntitiesInRange(coordinates, args.Range, _crew);

        var found = 0;
        foreach (var crew in _crew)
        {
            if (!_mobState.IsAlive(crew))
                continue;

            Mark(crew, args.Duration);
            found++;
        }

        if (args.Effect is { } effect)
            Spawn(effect, coordinates);

        _audio.PlayPvs(args.Sound, ent);
        _popup.PopupEntity(Loc.GetString("replicator-scan-result", ("count", found)), ent, ent);
        args.Handled = true;
    }

    private void Mark(EntityUid target, TimeSpan duration)
    {
        var fresh = !HasComp<ReplicatorMarkedComponent>(target);
        var marked = EnsureComp<ReplicatorMarkedComponent>(target);
        var end = _timing.CurTime + duration;
        if (end > marked.EndTime)
        {
            marked.EndTime = end;
            Dirty(target, marked);
        }

        if (fresh)
            _pvsOverride.AddGlobalOverride(target);
    }

    private void OnMarkShutdown(Entity<ReplicatorMarkedComponent> ent, ref ComponentShutdown args)
    {
        _pvsOverride.RemoveGlobalOverride(ent);
    }

    #endregion
}

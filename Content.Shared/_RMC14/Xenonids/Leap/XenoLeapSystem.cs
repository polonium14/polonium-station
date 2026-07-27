using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Invisibility;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Leap;

public sealed class XenoLeapSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoPlasmaSystem _plasma = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();

        SubscribeLocalEvent<XenoLeapComponent, XenoLeapActionEvent>(OnLeapAction);
        SubscribeLocalEvent<XenoLeapComponent, XenoLeapDoAfterEvent>(OnLeapDoAfter);

        SubscribeLocalEvent<XenoLeapingComponent, StartCollideEvent>(OnLeapingCollide);
        SubscribeLocalEvent<XenoLeapingComponent, LandEvent>(OnLeapingLand);
        SubscribeLocalEvent<XenoLeapingComponent, StopThrowEvent>(OnLeapingStopThrow);
        SubscribeLocalEvent<XenoLeapingComponent, ComponentRemove>(OnLeapingRemove);
        SubscribeLocalEvent<XenoLeapingComponent, PhysicsSleepEvent>(OnLeapingSleep);
    }

    private void OnLeapAction(Entity<XenoLeapComponent> xeno, ref XenoLeapActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (HasComp<XenoLeapingComponent>(xeno))
            return;

        if (xeno.Comp.PlasmaCost > FixedPoint2.Zero &&
            !_plasma.HasPlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
        {
            return;
        }

        args.Handled = true;

        var ev = new XenoLeapDoAfterEvent(GetNetCoordinates(args.Target));
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.Delay, ev, xeno)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DamageThreshold = FixedPoint2.New(10),
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnLeapDoAfter(Entity<XenoLeapComponent> xeno, ref XenoLeapDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-leap-cancelled"), xeno, xeno);
            return;
        }

        if (_net.IsClient)
            return;

        if (!_physicsQuery.TryGetComponent(xeno, out var physics))
            return;

        if (EnsureComp<XenoLeapingComponent>(xeno, out var leaping))
            return;

        if (xeno.Comp.PlasmaCost > FixedPoint2.Zero &&
            !_plasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
        {
            RemCompDeferred<XenoLeapingComponent>(xeno);
            return;
        }

        args.Handled = true;

        var origin = _transform.GetMapCoordinates(xeno);
        var target = _transform.ToMapCoordinates(args.Coordinates);
        var direction = target.Position - origin.Position;

        if (direction == Vector2.Zero)
        {
            RemCompDeferred<XenoLeapingComponent>(xeno);
            return;
        }

        var length = direction.Length();
        var distance = Math.Clamp(length, 0.1f, xeno.Comp.Range.Float());
        direction *= distance / length;

        leaping.Origin = _transform.GetMoverCoordinates(xeno);
        leaping.ParalyzeTime = xeno.Comp.KnockdownTime;
        leaping.LeapSound = xeno.Comp.LeapSound;
        leaping.MoveDelayTime = xeno.Comp.MoveDelayTime;
        leaping.KnockdownRequiresInvisibility = xeno.Comp.KnockdownRequiresInvisibility;
        leaping.IgnoredCollisionGroup = xeno.Comp.IgnoredCollisionGroup;
        leaping.LeapEndTime = _timing.CurTime + TimeSpan.FromSeconds(direction.Length() / xeno.Comp.Strength);
        Dirty(xeno, leaping);

        if (_fixturesQuery.TryGetComponent(xeno, out var fixtures) && fixtures.Fixtures.Count > 0)
        {
            var fixture = fixtures.Fixtures.First();
            var mask = fixture.Value.CollisionMask ^ (int) leaping.IgnoredCollisionGroup;
            _physics.SetCollisionMask(xeno, fixture.Key, fixture.Value, mask);
        }

        _throwing.TryThrow(
            xeno,
            direction,
            xeno.Comp.Strength,
            xeno,
            pushbackRatio: 0,
            recoil: false,
            doSpin: false);

        // same-tile / already touching
        foreach (var ent in _physics.GetContactingEntities(xeno.Owner, physics))
        {
            if (_hive.FromSameHive(xeno.Owner, ent))
                continue;

            if (ApplyLeapHit((xeno, leaping), ent))
                return;
        }
    }

    private void OnLeapingCollide(Entity<XenoLeapingComponent> xeno, ref StartCollideEvent args)
    {
        if (_net.IsClient)
            return;

        ApplyLeapHit(xeno, args.OtherEntity);
    }

    private void OnLeapingLand(Entity<XenoLeapingComponent> ent, ref LandEvent args)
    {
        if (_net.IsClient)
            return;

        StopLeap(ent);
    }

    private void OnLeapingStopThrow(Entity<XenoLeapingComponent> ent, ref StopThrowEvent args)
    {
        if (_net.IsClient)
            return;

        StopLeap(ent);
    }

    private void OnLeapingRemove(Entity<XenoLeapingComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsServer)
            RestorePhysics(ent);
    }

    private void OnLeapingSleep(Entity<XenoLeapingComponent> ent, ref PhysicsSleepEvent args)
    {
        if (_net.IsClient)
            return;

        StopLeap(ent);
    }

    private bool ApplyLeapHit(Entity<XenoLeapingComponent> xeno, EntityUid target)
    {
        if (xeno.Comp.KnockedDown)
            return false;

        if (target == xeno.Owner)
            return false;

        if (!HasComp<MobStateComponent>(target))
            return false;

        if (_standing.IsDown(target) || _mobState.IsIncapacitated(target))
            return false;

        if (HasComp<XenoComponent>(target) || HasComp<XenoFriendlyComponent>(target))
        {
            if (_hive.FromSameHive(xeno.Owner, target))
            {
                StopLeap(xeno);
                return true;
            }
        }

        if (HasComp<XenoComponent>(target))
            return false;

        xeno.Comp.KnockedDown = true;
        Dirty(xeno);

        if (_physicsQuery.TryGetComponent(xeno, out var physics))
            _physics.SetBodyStatus(xeno, physics, BodyStatus.OnGround);

        var canKnockdown = !xeno.Comp.KnockdownRequiresInvisibility ||
                           (TryComp(xeno, out XenoInvisibilityComponent? invis) && invis.Active);

        if (canKnockdown)
        {
            if (_net.IsServer)
                _stun.TryUpdateParalyzeDuration(target, xeno.Comp.ParalyzeTime);
        }

        if (!xeno.Comp.PlayedSound)
        {
            xeno.Comp.PlayedSound = true;
            Dirty(xeno);
            _audio.PlayPredicted(xeno.Comp.LeapSound, xeno, xeno);
        }

        StopLeap(xeno);
        return true;
    }

    private void StopLeap(Entity<XenoLeapingComponent> leaping)
    {
        if (_physicsQuery.TryGetComponent(leaping, out var physics))
        {
            _physics.SetLinearVelocity(leaping, Vector2.Zero, body: physics);
            _physics.SetBodyStatus(leaping, physics, BodyStatus.OnGround);
        }

        RestorePhysics(leaping);
        RemCompDeferred<XenoLeapingComponent>(leaping);
    }

    private void RestorePhysics(Entity<XenoLeapingComponent> leaping)
    {
        if (!_fixturesQuery.TryGetComponent(leaping, out var fixtures) || fixtures.Fixtures.Count == 0)
            return;

        var fixture = fixtures.Fixtures.First();
        var mask = fixture.Value.CollisionMask | (int) leaping.Comp.IgnoredCollisionGroup;
        _physics.SetCollisionMask(leaping, fixture.Key, fixture.Value, mask);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var leaping = EntityQueryEnumerator<XenoLeapingComponent>();
        while (leaping.MoveNext(out var uid, out var comp))
        {
            if (time < comp.LeapEndTime)
                continue;

            StopLeap((uid, comp));
        }
    }
}

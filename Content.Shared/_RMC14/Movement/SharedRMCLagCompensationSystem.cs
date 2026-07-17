// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._RMC14.CCVar;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Movement;

public abstract partial class SharedRMCLagCompensationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public float MarginTiles { get; private set; }

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;
    private int _substeps;
    private float _substepTime;

    private readonly Dictionary<NetUserId, GameTick> _lastRealTicks = new();

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();

        SubscribeNetworkEvent<RMCSetLastRealTickEvent>(OnSetLastRealTick);

        Subs.CVar(_config, RMCCVars.RMCLagCompensationMarginTiles, v => MarginTiles = v, true);
        Subs.CVar(_config, CVars.NetTickrate, UpdateSubsteps, true);
        Subs.CVar(_config, CVars.TargetMinimumTickrate, UpdateSubsteps, true);
    }

    private void OnSetLastRealTick(RMCSetLastRealTickEvent msg, EntitySessionEventArgs args)
    {
        SetLastRealTick(args.SenderSession.UserId, msg.Tick - 1);
    }

    private void UpdateSubsteps(int _)
    {
        var targetMinTickrate = (float) _config.GetCVar(CVars.TargetMinimumTickrate);
        var serverTickrate = (float) _config.GetCVar(CVars.NetTickrate);
        _substeps = (int) Math.Ceiling(targetMinTickrate / serverTickrate);
        _substepTime = 1.0f / serverTickrate / _substeps;
    }

    public virtual (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngle(
        EntityUid uid,
        ICommonSession? pSession,
        TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return (EntityCoordinates.Invalid, Angle.Zero);

        return (xform.Coordinates, xform.LocalRotation);
    }

    public virtual Angle GetAngle(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (_, angle) = GetCoordinatesAngle(uid, session, xform);
        return angle;
    }

    public virtual EntityCoordinates GetCoordinates(
        EntityUid uid,
        ICommonSession? session,
        TransformComponent? xform = null)
    {
        var (coordinates, _) = GetCoordinatesAngle(uid, session, xform);
        return coordinates;
    }

    public EntityCoordinates GetCoordinates(EntityUid uid, EntityUid? session, TransformComponent? xform = null)
    {
        if (!_actorQuery.TryComp(session, out var actor))
            return GetCoordinates(uid, (ICommonSession?) null, xform);

        return GetCoordinates(uid, actor.PlayerSession, xform);
    }

    public virtual GameTick GetLastRealTick(NetUserId? session)
    {
        return session == null ? _timing.CurTick : _lastRealTicks.GetValueOrDefault(session.Value, _timing.CurTick);
    }

    public void SetLastRealTick(NetUserId session, GameTick tick)
    {
        if (_net.IsClient)
            return;

        _lastRealTicks[session] = tick;
    }

    public void SendLastRealTick()
    {
        if (_net.IsServer)
            return;

        RaiseNetworkEvent(new RMCSetLastRealTickEvent(GetLastRealTick(null)));
    }

    public bool Collides(
        Entity<FixturesComponent?> target,
        Entity<PhysicsComponent?> projectile,
        ICommonSession? perspectiveSession,
        int substep = 0)
    {
        if (!Resolve(target, ref target.Comp, false) ||
            !Resolve(projectile, ref projectile.Comp, false))
        {
            return false;
        }

        substep = Math.Clamp(substep, -_substeps, _substeps);

        var projectileCoordinates = _transform.GetMapCoordinates(projectile);
        var projectileVelocity = _physics.GetLinearVelocity(projectile, projectile.Comp.LocalCenter);
        var substeppedProjectilePos = projectileCoordinates.Position +
                                        (projectileVelocity / _timing.TickRate) * (substep / (float) _substeps);

        var targetCoordinates = _transform.ToMapCoordinates(GetCoordinates(target, perspectiveSession));
        var transform = new Transform(targetCoordinates.Position, 0);
        var targetBounds = new Box2(transform.Position, transform.Position);

        foreach (var fixture in target.Comp.Fixtures.Values)
        {
            if ((fixture.CollisionLayer & projectile.Comp.CollisionMask) == 0)
                continue;

            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                var boundy = fixture.Shape.ComputeAABB(transform, i);
                targetBounds = targetBounds.Union(boundy);
            }
        }

        var projectileTransform = new Transform(substeppedProjectilePos, 0);
        var projectileBounds = new Box2(projectileTransform.Position, projectileTransform.Position);

        if (_fixturesQuery.TryComp(projectile, out var projFixtureComp))
        {
            foreach (var fixture in projFixtureComp.Fixtures.Values)
            {
                for (var i = 0; i < fixture.Shape.ChildCount; i++)
                {
                    var boundy = fixture.Shape.ComputeAABB(projectileTransform, i);
                    projectileBounds = projectileBounds.Union(boundy);
                }
            }
        }

        if (targetBounds.Intersects(projectileBounds))
            return true;

        var xDist = Math.Max(targetBounds.Left - projectileBounds.Right, projectileBounds.Left - targetBounds.Right);
        var yDist = Math.Max(targetBounds.Bottom - projectileBounds.Top, projectileBounds.Bottom - targetBounds.Top);
        xDist = Math.Max(0, xDist);
        yDist = Math.Max(0, yDist);
        var aabbDistance = xDist * xDist + yDist * yDist;

        return aabbDistance <= MarginTiles * MarginTiles;
    }
}

// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Projectiles;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Client.GameStates;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Weapons.Ranged.Prediction;

public sealed partial class GunPredictionSystem : SharedGunPredictionSystem
{
    [Dependency] private IClientGameStateManager _gameState = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ProjectileSystem _projectile = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<IgnorePredictionHideComponent> _ignorePredictionHideQuery;
    private EntityQuery<IgnorePredictionHitComponent> _ignorePredictionHitQuery;
    private EntityQuery<PredictedProjectileClientComponent> _predictedClientQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();

        _ignorePredictionHideQuery = GetEntityQuery<IgnorePredictionHideComponent>();
        _ignorePredictionHitQuery = GetEntityQuery<IgnorePredictionHitComponent>();
        _predictedClientQuery = GetEntityQuery<PredictedProjectileClientComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnBeforeSolve);
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnAfterSolve);
        SubscribeLocalEvent<RequestShootEvent>(OnShootRequest);

        SubscribeLocalEvent<PredictedProjectileClientComponent, UpdateIsPredictedEvent>(OnClientProjectileUpdateIsPredicted);
        SubscribeLocalEvent<PredictedProjectileClientComponent, StartCollideEvent>(OnClientProjectileStartCollide);

        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentAdd>(OnServerProjectileAdd);
        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentStartup>(OnServerProjectileStartup);
        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentRemove>(OnServerProjectileRemove);

        UpdatesBefore.Add(typeof(TransformSystem));
    }

    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent ev)
    {
        var query = EntityQueryEnumerator<PredictedProjectileClientComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
            predicted.Coordinates = Transform(uid).Coordinates;
        }
    }

    private void OnAfterSolve(ref PhysicsUpdateAfterSolveEvent ev)
    {
        if (_timing.IsFirstTimePredicted)
        {
            var pending = EntityQueryEnumerator<PredictedProjectileClientComponent>();
            while (pending.MoveNext(out var uid, out var predicted))
            {
                if (predicted.PendingImpactTarget is not { } target)
                    continue;

                predicted.PendingImpactTarget = null;
                if (!Exists(target))
                    continue;

                var hit = new HashSet<(NetEntity, MapCoordinates)>
                {
                    (GetNetEntity(target), _transform.GetMapCoordinates(target)),
                };
                RaiseNetworkEvent(new PredictedProjectileHitEvent(uid.Id, hit, _transform.GetMapCoordinates(uid)));
            }

            return;
        }

        var query = EntityQueryEnumerator<PredictedProjectileClientComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
            if (predicted.Hit)
            {
                predicted.Coordinates = null;
                continue;
            }

            if (predicted.Coordinates is { } coordinates)
                _transform.SetCoordinates(uid, coordinates);

            predicted.Coordinates = null;
        }
    }

    private void OnShootRequest(RequestShootEvent ev, EntitySessionEventArgs args)
    {
        if (_timing.IsFirstTimePredicted)
            return;

        ShootRequested(ev.Gun, ev.Coordinates, ev.Target, ev.Shot, args.SenderSession, ev.RearmSemiAuto);
    }

    private void OnClientProjectileUpdateIsPredicted(Entity<PredictedProjectileClientComponent> ent, ref UpdateIsPredictedEvent args)
    {
        args.IsPredicted = true;
    }

    private void OnClientProjectileStartCollide(Entity<PredictedProjectileClientComponent> ent, ref StartCollideEvent args)
    {
        if (ent.Comp.Hit)
            return;

        // Only the actual projectile fixture counts as a hit. Ignore the "fly-by" sound sensor and
        // any soft/non-hard contacts (e.g. the ejected cartridge casing spawned at the muzzle),
        // matching SharedProjectileSystem.OnStartCollide.
        if (args.OurFixtureId != SharedProjectileSystem.ProjectileFixture || !args.OtherFixture.Hard)
            return;

        // Predicted hit effects (the red damage flash in particular) only apply during first-time
        // prediction. If this collision fires while re-predicting, don't consume the hit here; let the
        // Update loop (which always runs first-time-predicted) process it so the flash reliably shows.
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!TryComp(ent, out ProjectileComponent? projectile) ||
            !TryComp(ent, out PhysicsComponent? physics) ||
            _ignorePredictionHitQuery.HasComp(args.OtherEntity))
        {
            return;
        }

        // send the hit after the solve, the contact position is still short of where the body ends up
        ent.Comp.PendingImpactTarget = args.OtherEntity;

        // Process hit effects but do NOT delete the projectile here (predicted: true). Deleting a
        // client-side predicted projectile mid-flight leaves stale physics contacts that crash the
        // engine's ResetContacts during prediction reset. It is stopped/hidden here and cleaned up
        // safely in Update instead.
        _projectile.ProjectileCollide((ent, projectile, physics), args.OtherEntity, predicted: true);
        MarkClientHit(ent, ent.Comp, physics);
    }

    private void OnServerProjectileAdd(Entity<PredictedProjectileServerComponent> ent, ref ComponentAdd args)
    {
        HideServerProjectile(ent);
    }

    private void OnServerProjectileStartup(Entity<PredictedProjectileServerComponent> ent, ref ComponentStartup args)
    {
        HideServerProjectile(ent);
    }

    private void OnServerProjectileRemove(Entity<PredictedProjectileServerComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.ClientEnt != _player.LocalEntity)
        {
            ShowSprite(ent);
            return;
        }

        TryDeleteClientTwin(ent.Comp.ClientId);
        ShowSprite(ent);
    }

    private void HideServerProjectile(Entity<PredictedProjectileServerComponent> ent)
    {
        if (!GunPrediction || !_gameState.IsPredictionEnabled)
            return;

        if (IsClientSide(ent) || _predictedClientQuery.HasComp(ent))
            return;

        if (ent.Comp.ClientEnt != _player.LocalEntity)
            return;

        if (_ignorePredictionHideQuery.HasComp(ent))
            return;

        if (TryComp(ent, out ProjectileComponent? projectile) &&
            projectile.ProjectileSpent &&
            !projectile.DeleteOnCollide)
        {
            return;
        }

        HideSprite(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var serverProjectiles = EntityQueryEnumerator<PredictedProjectileServerComponent, SpriteComponent>();
        while (serverProjectiles.MoveNext(out var serverUid, out var server, out var serverSprite))
        {
            if (server.ClientEnt != _player.LocalEntity)
                continue;

            if (IsClientSide(serverUid) || _predictedClientQuery.HasComp(serverUid))
                continue;

            if (_ignorePredictionHideQuery.HasComp(serverUid))
                continue;

            if (!TryComp(serverUid, out ProjectileComponent? projectile))
            {
                _sprite.SetVisible((serverUid, serverSprite), true);
                continue;
            }

            if (projectile.ProjectileSpent && !projectile.DeleteOnCollide)
            {
                _sprite.SetVisible((serverUid, serverSprite), true);
                continue;
            }

            _sprite.SetVisible((serverUid, serverSprite), false);
        }

        // TODO gun prediction remove this once the client reliably detects collisions
        var projectiles = EntityQueryEnumerator<PredictedProjectileClientComponent, ProjectileComponent, PhysicsComponent>();
        while (projectiles.MoveNext(out var uid, out var predicted, out var projectile, out var physics))
        {
            if (predicted.Hit)
            {
                if (IsClientSide(uid))
                {
                    // hold the twin at the impact pose until the server arrow is shown
                    _physics.SetCanCollide(uid, false, body: physics);
                    continue;
                }

                RemComp<PredictedProjectileClientComponent>(uid);
                continue;
            }

            // Find a real hit: the projectile's own "projectile" fixture actually touching a hard
            // fixture. Using raw contacts (not GetContactingEntities) avoids the large "fly-by" sound
            // sensor reporting far-away walls (1-3 tiles ahead) as hits.
            EntityUid? hitEntity = null;
            var enumerator = _physics.GetContacts(uid);
            while (enumerator.MoveNext(out var contact))
            {
                if (!contact.IsTouching)
                    continue;

                string ourFixtureId;
                EntityUid other;
                Fixture? otherFixture;
                if (contact.EntityA == uid)
                {
                    ourFixtureId = contact.FixtureAId;
                    other = contact.EntityB;
                    otherFixture = contact.FixtureB;
                }
                else
                {
                    ourFixtureId = contact.FixtureBId;
                    other = contact.EntityA;
                    otherFixture = contact.FixtureA;
                }

                if (ourFixtureId != SharedProjectileSystem.ProjectileFixture)
                    continue;

                if (otherFixture is not { Hard: true })
                    continue;

                if (_ignorePredictionHitQuery.HasComp(other))
                    continue;

                hitEntity = other;
                break;
            }

            if (hitEntity is not { } target)
                continue;

            var hit = new HashSet<(NetEntity, MapCoordinates)>
            {
                (GetNetEntity(target), _transform.GetMapCoordinates(target)),
            };

            var ev = new PredictedProjectileHitEvent(uid.Id, hit, _transform.GetMapCoordinates(uid));
            RaiseNetworkEvent(ev);

            _projectile.ProjectileCollide((uid, projectile, physics), target, predicted: true);
            MarkClientHit(uid, predicted, physics);
        }

        // Keep the shooter's authoritative projectile hidden after a predicted hit.
        // Only hide when past the impact distance - never force-visible (that re-shows the lagging
        // server bullet as a second shot).
        var predictedQuery = EntityQueryEnumerator<PredictedProjectileHitComponent, SpriteComponent, TransformComponent>();
        while (predictedQuery.MoveNext(out var uid, out var hit, out var sprite, out var xform))
        {
            if (IsClientSide(uid) || _predictedClientQuery.HasComp(uid))
                continue;

            var origin = hit.Origin;
            var coordinates = xform.Coordinates;
            if (!origin.TryDistance(EntityManager, _transform, coordinates, out var distance) ||
                distance >= hit.Distance)
            {
                _sprite.SetVisible((uid, sprite), false);
            }
        }
    }

    private void TryDeleteClientTwin(int clientId)
    {
        var twin = new EntityUid(clientId);

        if (!Exists(twin) || !IsClientSide(twin) || !_predictedClientQuery.HasComp(twin))
            return;

        HideSprite(twin);

        if (TryComp(twin, out PhysicsComponent? physics))
            _physics.SetCanCollide(twin, false, body: physics);

        QueueDel(twin);
    }

    private void HideSprite(EntityUid uid)
    {
        if (_spriteQuery.TryComp(uid, out var sprite))
            _sprite.SetVisible((uid, sprite), false);
    }

    private void ShowSprite(EntityUid uid)
    {
        if (_spriteQuery.TryComp(uid, out var sprite))
            _sprite.SetVisible((uid, sprite), true);
    }

    private void MarkClientHit(EntityUid uid, PredictedProjectileClientComponent predicted, PhysicsComponent physics)
    {
        if (predicted.Hit)
            return;

        predicted.Hit = true;

        _physics.SetAngularVelocity(uid, 0f, body: physics);
        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        _physics.SetCanCollide(uid, false, body: physics);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // TODO bullet prediction remove this when lerping doesnt make the client's entity slightly slower
        var projectiles = EntityQueryEnumerator<PredictedProjectileClientComponent, TransformComponent>();
        while (projectiles.MoveNext(out _, out var xform))
        {
            xform.ActivelyLerping = false;
        }
    }
}

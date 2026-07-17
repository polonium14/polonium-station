// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
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
            return;
        var query = EntityQueryEnumerator<PredictedProjectileClientComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
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

        var netEnt = GetNetEntity(args.OtherEntity);
        var pos = _transform.GetMapCoordinates(args.OtherEntity);
        var hit = new HashSet<(NetEntity, MapCoordinates)> { (netEnt, pos) };
        var ev = new PredictedProjectileHitEvent(ent.Owner.Id, hit);
        RaiseNetworkEvent(ev);

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

    private void HideServerProjectile(Entity<PredictedProjectileServerComponent> ent)
    {
        if (!GunPrediction || !_gameState.IsPredictionEnabled)
            return;

        // Never hide our own client-side prediction entity.
        if (IsClientSide(ent) || _predictedClientQuery.HasComp(ent))
            return;

        if (ent.Comp.ClientEnt != _player.LocalEntity)
            return;

        if (_ignorePredictionHideQuery.HasComp(ent))
            return;

        if (_spriteQuery.TryComp(ent, out var sprite))
            _sprite.SetVisible((ent, sprite), false);
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

            _sprite.SetVisible((serverUid, serverSprite), false);
        }

        // TODO gun prediction remove this once the client reliably detects collisions
        var projectiles = EntityQueryEnumerator<PredictedProjectileClientComponent, ProjectileComponent, PhysicsComponent>();
        while (projectiles.MoveNext(out var uid, out var predicted, out var projectile, out var physics))
        {
            if (predicted.Hit)
            {
                // The projectile already registered a hit. Cleanly remove it from the physics contact
                // graph (SetCanCollide false destroys its contacts on both sides) before deleting, so the
                // engine's ResetContacts never dereferences a stale contact for a deleted entity.
                _physics.SetCanCollide(uid, false, body: physics);
                QueueDel(uid);
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

            var ev = new PredictedProjectileHitEvent(uid.Id, hit);
            RaiseNetworkEvent(ev);

            _projectile.ProjectileCollide((uid, projectile, physics), target, predicted: true);
            MarkClientHit(uid, predicted, physics);
        }

        // Keep the shooter's authoritative projectile hidden after a predicted hit.
        // Only hide when past the impact distance — never force-visible (that re-shows the lagging
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

    private void MarkClientHit(EntityUid uid, PredictedProjectileClientComponent predicted, PhysicsComponent physics)
    {
        if (predicted.Hit)
            return;

        // Freeze and hide the predicted projectile at the impact point. Actual deletion happens next
        // tick in Update after its physics contacts are torn down, to avoid a mid-flight delete crash.
        predicted.Hit = true;
        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);

        if (_spriteQuery.TryComp(uid, out var sprite))
            _sprite.SetVisible((uid, sprite), false);
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

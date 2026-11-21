using System.Numerics;
using Content.Shared._Funkystation.Explosion.Components;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Funkystation.Explosion.EntitySystems;

public abstract partial class SharedExplosionEffectSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private ThrowingSystem _throwing = null!;

    // Explosives can trigger while the engine enumerates a grid's children during a map change,
    // and spawning at that moment aborts the grid move.
    private readonly List<PendingEffect> _pendingEffects = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExplosionEffectComponent, ExplosiveTriggeredEvent>(OnExplosionEffectTriggered);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var count = _pendingEffects.Count;
        for (var i = 0; i < count; i++)
        {
            var pending = _pendingEffects[i];
            if (TerminatingOrDeleted(pending.Coordinates.EntityId))
                continue;

            var spawned = SpawnAtPosition(pending.Prototype, pending.Coordinates);
            if (pending.ThrowDirection is { } direction && !TerminatingOrDeleted(spawned))
                _throwing.TryThrow(spawned, direction, pending.ThrowSpeed);
        }

        _pendingEffects.RemoveRange(0, count);
    }

    private void OnExplosionEffectTriggered(Entity<ExplosionEffectComponent> ent, ref ExplosiveTriggeredEvent args)
    {
        // avoid spawning/throwing during deletion
        if (TerminatingOrDeleted(ent))
            return;

        var coordinates = _transform.GetMoverCoordinates(ent);

        foreach (var effect in ent.Comp.VisualEffects)
        {
            _pendingEffects.Add(new PendingEffect(effect, coordinates));
        }

        if (ent.Comp.MaxShrapnel > 0)
        {
            foreach (var effect in ent.Comp.ShrapnelEffects)
            {
                var shrapnelCount = _random.Next(ent.Comp.MinShrapnel, ent.Comp.MaxShrapnel);
                for (var i = 0; i < shrapnelCount; i++)
                {
                    var angle = _random.NextAngle();
                    var direction = angle.ToVec().Normalized() * 10;
                    _pendingEffects.Add(new PendingEffect(effect, coordinates, direction, ent.Comp.ShrapnelSpeed / 10));
                }
            }
        }
    }

    private readonly record struct PendingEffect(
        EntProtoId Prototype,
        EntityCoordinates Coordinates,
        Vector2? ThrowDirection = null,
        float ThrowSpeed = 0f);
}

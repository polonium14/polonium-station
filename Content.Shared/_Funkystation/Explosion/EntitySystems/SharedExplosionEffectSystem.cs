using Content.Shared._Funkystation.Explosion.Components;
using Content.Shared.Throwing;
using Robust.Shared.Random;

namespace Content.Shared._Funkystation.Explosion.EntitySystems;

public abstract partial class SharedExplosionEffectSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private ThrowingSystem _throwing = null!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExplosionEffectComponent, ExplosiveTriggeredEvent>(OnExplosionEffectTriggered);
    }

    private void OnExplosionEffectTriggered(Entity<ExplosionEffectComponent> ent, ref ExplosiveTriggeredEvent args)
    {
        // avoid spawning/throwing during deletion
        if (TerminatingOrDeleted(ent))
            return;

        foreach (var effect in ent.Comp.VisualEffects)
        {
            SpawnNextToOrDrop(effect, ent);
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
                    var shrapnel = SpawnNextToOrDrop(effect, ent);
                    if (Exists(shrapnel) && !TerminatingOrDeleted(shrapnel))
                    {
                        _throwing.TryThrow(shrapnel, direction, ent.Comp.ShrapnelSpeed / 10);
                    }
                }
            }
        }
    }
}

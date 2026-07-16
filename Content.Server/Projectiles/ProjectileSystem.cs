using Content.Server.Destructible;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;

namespace Content.Server.Projectiles;

public sealed partial class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    protected override FixedPoint2 GetProjectileDamageRequired(EntityUid target)
    {
        var damageRequired = _destructible.DestroyedAt(target);

        if (TryComp<DamageableComponent>(target, out var damageable))
        {
            damageRequired -= _damageable.GetTotalDamage((target, damageable));
            damageRequired = FixedPoint2.Max(damageRequired, FixedPoint2.Zero);
        }

        return damageRequired;
    }
}

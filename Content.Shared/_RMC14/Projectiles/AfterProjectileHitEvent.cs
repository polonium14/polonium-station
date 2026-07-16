using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;

namespace Content.Shared._RMC14.Projectiles;

[ByRefEvent]
public record struct AfterProjectileHitEvent(Entity<ProjectileComponent> Projectile, EntityUid Target);

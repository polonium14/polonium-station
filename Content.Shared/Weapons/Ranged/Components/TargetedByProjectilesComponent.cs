using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared.Weapons.Ranged.Components;

[RegisterComponent]
[Access(typeof(SharedGunSystem))]
public sealed partial class TargetedByProjectilesComponent : Component
{
    public HashSet<EntityUid> Projectiles = new();
}

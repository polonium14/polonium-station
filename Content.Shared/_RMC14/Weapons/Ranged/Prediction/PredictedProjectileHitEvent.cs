using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

[Serializable, NetSerializable]
public sealed class PredictedProjectileHitEvent(
    int projectile,
    // Polonium: the client predicts only the first hit
    NetEntity target,
    MapCoordinates targetCoordinates,
    MapCoordinates impact) : EntityEventArgs
{
    public readonly int Projectile = projectile;
    public readonly NetEntity Target = target;
    public readonly MapCoordinates TargetCoordinates = targetCoordinates;

    // where athe shooter already saw the projectile stop
    public readonly MapCoordinates Impact = impact;
}

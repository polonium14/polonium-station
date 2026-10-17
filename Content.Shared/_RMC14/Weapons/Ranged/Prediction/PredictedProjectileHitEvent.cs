using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

[Serializable, NetSerializable]
public sealed class PredictedProjectileHitEvent(
    int projectile,
    HashSet<(NetEntity Id, MapCoordinates Coordinates)> hit,
    MapCoordinates impact) : EntityEventArgs
{
    public readonly int Projectile = projectile;
    public readonly HashSet<(NetEntity Id, MapCoordinates Coordinates)> Hit = hit;

    // where athe shooter already saw the projectile stop
    public readonly MapCoordinates Impact = impact;
}

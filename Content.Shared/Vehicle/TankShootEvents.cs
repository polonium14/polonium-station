using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared.Vehicles;

[Serializable, NetSerializable]
public sealed class TankShootEvent : EntityEventArgs
{
    public bool IsMain;
    public Angle AimAngle;
    public Vector2 WorldPosition;

    public TankShootEvent(bool isMain, Angle aimAngle, Vector2 worldPosition)
    {
        IsMain = isMain;
        AimAngle = aimAngle;
        WorldPosition = worldPosition;
    }
}
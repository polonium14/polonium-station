using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Vehicles;

/// <summary>
/// Klient → serwer: prośba o strzał.
/// AimAngle = kierunek do kursora, WorldPosition = pozycja czołgu.
/// </summary>
[Serializable, NetSerializable]
public sealed class TankShootEvent : EntityEventArgs
{
    /// <summary>True = karabin (R), False = działo (F).</summary>
    public bool MachineGun { get; set; }

    /// <summary>Kąt wieży / kierunek do myszy.</summary>
    public Angle AimAngle { get; set; }

    /// <summary>Światowa pozycja środka czołgu w momencie strzału.</summary>
    public Vector2 WorldPosition { get; set; }

    public TankShootEvent(bool machineGun, Angle aimAngle, Vector2 worldPosition)
    {
        MachineGun = machineGun;
        AimAngle = aimAngle;
        WorldPosition = worldPosition;
    }
}
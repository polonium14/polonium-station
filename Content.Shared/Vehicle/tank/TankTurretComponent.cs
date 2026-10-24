using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Vehicles;

/// <summary>
/// Wieża: celowanie, strzały, pancerz, kadłub.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TankTurretComponent : Component
{
    /// <summary>Kąt wieży = kierunek do myszy (strzał).</summary>
    [DataField, AutoNetworkedField]
    public Angle TurretAngle;

    /// <summary>Offset tylko pod grafikę lufy. Nie wpływa na kierunek pocisku.</summary>
    [DataField]
    public float AimOffset = 90f;

    /// <summary>Szybkość obrotu wieży.</summary>
    [DataField]
    public float RotateSpeed = 5f;

    [DataField, AutoNetworkedField]
    public EntProtoId MainProjectile = "BulletRocket";

    [DataField, AutoNetworkedField]
    public EntProtoId MgProjectile = "BulletLightRifle";

    [DataField, AutoNetworkedField]
    public float MainFireRate = 19f;

    [DataField, AutoNetworkedField]
    public float MgFireRate = 0.12f;

    [DataField, AutoNetworkedField]
    public float MgBurstDuration = 11f;

    [DataField, AutoNetworkedField]
    public float MgReloadTime = 5f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextMainFire = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextMgFire = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan MgBurstEnd = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan MgReloadEnd = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public bool MgReloading;

    [DataField, AutoNetworkedField]
    public bool MainReloading;

    /// <summary>Limit spamu popup o CD (co 1 s).</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextCdPopup = TimeSpan.Zero;

    [DataField]
    public Vector2 MuzzleLocalOffset = new(0f, 2.2f);

    [DataField]
    public Vector2 MgMuzzleLocalOffset = new(0f, 1.7f);

    [DataField]
    public SoundSpecifier? SoundMain;

    [DataField]
    public SoundSpecifier? SoundMg;

    [DataField, AutoNetworkedField]
    public float MaxArmor = 200f;

    [DataField, AutoNetworkedField]
    public float ArmorDamage;

    [DataField, AutoNetworkedField]
    public bool ArmorDestroyed;

    [DataField, AutoNetworkedField]
    public float MaxHull = 400f;

    [DataField, AutoNetworkedField]
    public float HullDamage;

    [DataField, AutoNetworkedField]
    public bool PanelsOpen;
}
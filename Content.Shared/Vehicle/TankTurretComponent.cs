using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Vehicles;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TankTurretComponent : Component
{
    [DataField, AutoNetworkedField]
    public Angle TurretAngle;

    [DataField, AutoNetworkedField]
    public float AimOffset = 90f;

    [DataField, AutoNetworkedField]
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

    [DataField, AutoNetworkedField]
    public float AccumulatedDamage;

    [DataField]
    public float MaxDamage = 200f;

    [DataField]
    public SoundSpecifier? SoundMain;

    [DataField]
    public SoundSpecifier? SoundMg;

    [DataField]
    public SoundSpecifier? SoundEngine;
}
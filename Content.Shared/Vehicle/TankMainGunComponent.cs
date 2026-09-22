using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Vehicles;

[RegisterComponent, NetworkedComponent]
public sealed partial class TankMainGunComponent : Component
{
    [DataField]
    public EntProtoId Projectile = "BulletRifle";

    [DataField]
    public float FireRate = 0.5f;

    [DataField]
    public TimeSpan NextFire = TimeSpan.Zero;

    [DataField]
    public SoundSpecifier? SoundGunshot;
}
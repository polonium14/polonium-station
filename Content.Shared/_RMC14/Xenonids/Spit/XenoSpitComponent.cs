using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Spit;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSpitSystem))]
public sealed partial class XenoSpitComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Projectile = "BulletAcid";

    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 25;

    [DataField, AutoNetworkedField]
    public float ProjectileSpeed = 20f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("XenoSpitAcid", AudioParams.Default.WithVolume(-10f));
}

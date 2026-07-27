using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Collision.Shapes;

namespace Content.Shared._RMC14.Xenonids.Fortify;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoFortifySystem))]
public sealed partial class XenoFortifyComponent : Component
{
    public const string FixtureId = "cm-xeno-fortify";

    [DataField, AutoNetworkedField]
    public bool Fortified;

    // RMC does armor - we just cut incoming dmg
    [DataField, AutoNetworkedField]
    public float DamageModifier = 0.4f;

    [DataField]
    public IPhysShape Shape = new PhysShapeCircle(0.49f);

    [DataField, AutoNetworkedField]
    public bool CanMoveFortified;

    [DataField, AutoNetworkedField]
    public bool CanHeadbuttFortified;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MoveSpeedModifier = FixedPoint2.New(0.45);

    [DataField, AutoNetworkedField]
    public SoundSpecifier FortifySound = new SoundPathSpecifier(
        "/Audio/Effects/stonedoor_openclose.ogg",
        AudioParams.Default.WithVariation(0.2f));
}

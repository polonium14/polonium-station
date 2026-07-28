using Content.Shared.FixedPoint;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Leap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoLeapSystem))]
public sealed partial class XenoLeapComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = FixedPoint2.Zero;

    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(0);

    [DataField, AutoNetworkedField]
    public FixedPoint2 Range = 6;

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public SoundSpecifier? LeapSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_pounce.ogg");

    [DataField, AutoNetworkedField]
    public float Strength = 20f;

    [DataField, AutoNetworkedField]
    public TimeSpan MoveDelayTime = TimeSpan.FromSeconds(0.7);

    [DataField, AutoNetworkedField]
    public bool KnockdownRequiresInvisibility;

    [DataField, AutoNetworkedField]
    public CollisionGroup IgnoredCollisionGroup = CollisionGroup.MidImpassable;
}

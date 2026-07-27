using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using static Robust.Shared.Utility.SpriteSpecifier;

namespace Content.Shared._RMC14.Xenonids.Pheromones;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoPheromonesSystem))]
public sealed partial class XenoFrenzyPheromonesComponent : Component
{
    [DataField, AutoNetworkedField]
    public SpriteSpecifier Icon = new Rsi(new ResPath("/Textures/_RMC14/Interface/xeno_pheromones_hud.rsi"), "frenzy");

    [DataField, AutoNetworkedField]
    public FixedPoint2 Multiplier;

    [DataField, AutoNetworkedField]
    public float AttackDamageAddPerMult = 2f;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MovementSpeedModifier = 0.06;
}

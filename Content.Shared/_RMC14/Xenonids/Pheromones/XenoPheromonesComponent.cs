using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Pheromones;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(XenoPheromonesSystem))]
public sealed partial class XenoPheromonesComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PheromonesPlasmaCost = 70;

    [DataField, AutoNetworkedField]
    public FixedPoint2 PheromonesPlasmaUpkeep = 10;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextPheromonesPlasmaUse;

    [DataField, AutoNetworkedField]
    public float PheromonesRange = 8f;

    // strength of aura - queen 4 drone 2 etc
    [DataField, AutoNetworkedField]
    public FixedPoint2 PheromonesMultiplier = 1;

    [DataField, AutoNetworkedField]
    public string? PheroSuffix;
}

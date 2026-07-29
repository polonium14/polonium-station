using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Evolution;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(XenoEvolutionSystem))]
public sealed partial class XenoEvolutionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool RequiresGranter = true;

    [DataField, AutoNetworkedField]
    public bool CanEvolveWithoutGranter;

    [DataField, AutoNetworkedField]
    public List<EntProtoId> EvolvesTo = new();

    [DataField, AutoNetworkedField]
    public List<EntProtoId> EvolvesToWithoutPoints = new();

    [DataField, AutoNetworkedField]
    public TimeSpan EvolutionDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan QueenEvolutionDelay = TimeSpan.FromSeconds(17.5);

    [DataField, AutoNetworkedField]
    public FixedPoint2 Points;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Max;

    [DataField, AutoNetworkedField]
    public FixedPoint2 PointsPerSecond = 0.5;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LastPointsAt;

    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "ActionXenoEvolve";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    [DataField, AutoNetworkedField]
    public bool GotPopup;
}

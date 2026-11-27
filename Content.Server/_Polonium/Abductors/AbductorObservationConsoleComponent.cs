using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// Lets an abductor look at the station through its own cameras and run the ship's teleporter from there.
/// </summary>
[RegisterComponent, Access(typeof(AbductorObservationSystem))]
public sealed partial class AbductorObservationConsoleComponent : Component
{
    [DataField]
    public EntProtoId EyePrototype = "AbductorObservationEye";

    [DataField(required: true)]
    public List<EntProtoId> Actions = new();

    /// <summary>
    /// How long the warning shows at the landing spot before someone arrives.
    /// </summary>
    [DataField]
    public TimeSpan DropDelay = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan RetrieveDelay = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public readonly List<EntityUid> ActionEntities = new();

    [ViewVariables]
    public EntityUid? User;
}

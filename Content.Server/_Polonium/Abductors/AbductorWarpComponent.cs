using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// A visible teleport in progress. Whoever it carries arrives when the time runs out.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(AbductorShipSystem))]
public sealed partial class AbductorWarpComponent : Component
{
    [ViewVariables]
    public EntityUid? Subject;

    /// <summary>
    /// When set, the warp carries everyone standing on this telepad at the moment it fires.
    /// </summary>
    [ViewVariables]
    public EntityUid? Telepad;

    /// <summary>
    /// When set, the subject is a specimen being brought aboard this ship.
    /// </summary>
    [ViewVariables]
    public EntityUid? Ship;

    /// <summary>
    /// The abductor at the console who called the specimen up and arrives together with it.
    /// </summary>
    [ViewVariables]
    public EntityUid? Retriever;

    [ViewVariables]
    public EntityCoordinates Destination;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ArriveAt;
}

using Robust.Shared.Map;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// The research state of one abductor ship, kept on its grid.
/// </summary>
[RegisterComponent, Access(typeof(AbductorShipSystem))]
public sealed partial class AbductorShipComponent : Component
{
    [ViewVariables]
    public readonly List<EntityUid> Crew = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> CrewMinds = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> Catalogued = new();

    [ViewVariables]
    public EntityUid? StationGrid;

    [ViewVariables]
    public EntityUid? Marked;

    [ViewVariables]
    public EntityCoordinates? ReleasePoint;

    [ViewVariables]
    public EntityCoordinates? LastView;

    [ViewVariables]
    public int Experiments;

    [ViewVariables]
    public bool HeardFirstMark;

    [ViewVariables]
    public bool HeardFirstAboard;
}

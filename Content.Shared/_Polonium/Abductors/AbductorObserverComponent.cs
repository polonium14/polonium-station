using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Polonium.Abductors;

/// <summary>
/// Someone looking at the station through an observation console.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbductorObserverComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public EntityUid Eye;

    [DataField, AutoNetworkedField]
    public EntityUid Console;

    [AutoNetworkedField]
    public EntityUid? StationGrid;

    [AutoNetworkedField]
    public EntityCoordinates? ReleasePoint;
}

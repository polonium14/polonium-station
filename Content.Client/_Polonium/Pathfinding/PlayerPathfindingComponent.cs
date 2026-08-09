using Robust.Shared.GameObjects;

namespace Content.Client._Polonium.Pathfinding;

[RegisterComponent]
public sealed partial class PlayerPathfindingComponent : Component
{

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? Destination;

    /// <summary>TutorialAnchor id on the player's grid. Resolved each tick into <see cref="Destination"/>.</summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public string? DestinationAnchorId;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool Active = true;

    /// <summary>
    /// Cache aktualnej trasy obliczonej z algorytmu A star
    /// Zawiera listę koordynatów na pojedynczym gridie
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public List<Vector2i> CurrentPath = new();
}

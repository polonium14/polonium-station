using Robust.Shared.Map;

namespace Content.Server._Polonium.Tutorial;

/// <summary>Cannot be taken through <see cref="Doorways"/>. See <see cref="TutorialConfinementSystem"/>.</summary>
[RegisterComponent]
public sealed partial class TutorialConfinedComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Doorways = new();

    [ViewVariables]
    public LocId? Popup;

    /// <summary>Where the thing carrying him last stood clear of every doorway.</summary>
    [ViewVariables]
    public EntityCoordinates? LastInside;
}

using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Medical.BedInternals;

/// <summary>
/// Marks a gas tank as one that <see cref="BedInternalsComponent"/> beds draw with the given overlay.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BedTankVisualComponent : Component
{
    [DataField(required: true)]
    public BedTankVisual Visual;
}

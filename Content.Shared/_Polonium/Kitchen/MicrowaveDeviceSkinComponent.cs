using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Kitchen;

/// <summary>
/// Makes the microwave window look like this particular microwave where it differs from the stock one.
/// Anything left out keeps the stock look.
/// </summary>
[RegisterComponent]
public sealed partial class MicrowaveDeviceSkinComponent : Component
{
    /// <summary>
    /// Texture of the body, in the colours of the microwave's sprite.
    /// </summary>
    [DataField]
    public ResPath? Body;

    /// <summary>
    /// Texture of the chamber while the microwave runs, in the colour of its light.
    /// </summary>
    [DataField]
    public ResPath? Light;

    /// <summary>
    /// Text on the name plate. The message gets the stock name and version as "name" and "version".
    /// </summary>
    [DataField]
    public LocId? Plate;
}

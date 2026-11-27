using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Chemistry;

/// <summary>
/// Tells the window of a bar dispenser how to draw the device, so that it matches the dispenser's sprite.
/// </summary>
[RegisterComponent]
public sealed partial class BarDispenserSkinComponent : Component
{
    /// <summary>
    /// The look of the device in its window.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<BarDispenserSkinPrototype> Skin;
}

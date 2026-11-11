using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Crosshairs;

/// <summary>
/// A combat-mode crosshair drawn at the cursor. Gun crosshairs show the spread of the next shot and guns pick one with
/// <see cref="Weapons.Ranged.Components.GunCrosshairComponent"/>; everything else uses one static melee crosshair.
/// </summary>
[Prototype]
public sealed partial class CrosshairPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The figure drawn around the cursor.
    /// </summary>
    [DataField(required: true)]
    public CrosshairShape Shape;

    /// <summary>
    /// Whether the figure turns to face the shooter. Pixel and fan shapes ignore it.
    /// </summary>
    [DataField]
    public bool FaceShooter;
}

/// <summary>
/// Figures the client knows how to draw for a <see cref="CrosshairPrototype"/>.
/// </summary>
public enum CrosshairShape : byte
{
    Cross,
    TCross,
    Corners,
    Grid,
    Ring,
    DashedRing,
    Arcs,
    Cloud,
    Fan,
    PixelCross,
    Chevrons,
}

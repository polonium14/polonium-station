using Content.Shared._Polonium.Crosshairs;
using Content.Shared._Polonium.Weapons.Ranged.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Weapons.Ranged.Components;

/// <summary>
/// Picks the combat-mode crosshair of this gun. Guns without it use <see cref="DefaultCrosshair"/> and <see cref="DefaultPelletCrosshair"/>.
/// </summary>
[RegisterComponent]
[Access(typeof(GunCrosshairSystem))]
public sealed partial class GunCrosshairComponent : Component
{
    /// <summary>
    /// Crosshair of guns that do not pick their own, also used for every gun when the player turns on a single crosshair.
    /// </summary>
    public static readonly ProtoId<CrosshairPrototype> DefaultCrosshair = "Cross";

    /// <summary>
    /// Crosshair shown by default while the next round fires several pellets.
    /// </summary>
    public static readonly ProtoId<CrosshairPrototype> DefaultPelletCrosshair = "Arcs";

    /// <summary>
    /// Crosshair drawn while this gun is aimed.
    /// </summary>
    [DataField]
    public ProtoId<CrosshairPrototype> Crosshair = DefaultCrosshair;

    /// <summary>
    /// Crosshair drawn instead while the next round fires several pellets, or null to keep <see cref="Crosshair"/>.
    /// </summary>
    [DataField]
    public ProtoId<CrosshairPrototype>? PelletCrosshair = DefaultPelletCrosshair;
}

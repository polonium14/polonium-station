using Content.Shared._Polonium.Crosshairs;
using Content.Shared._Polonium.Weapons.Ranged.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Weapons.Ranged.Components;

/// <summary>
/// Crosshair this entity aims with when the gun does not pick its own, like the built-in targeting of robots.
/// </summary>
[RegisterComponent]
[Access(typeof(GunCrosshairSystem))]
public sealed partial class GunUserCrosshairComponent : Component
{
    /// <summary>
    /// Crosshair used instead of <see cref="GunCrosshairComponent.DefaultCrosshair"/>.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CrosshairPrototype> Crosshair;
}

// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Ranged.Components;

public sealed partial class BatteryWeaponFireMode
{
    /// <summary>
    /// Magazine visuals state used while this mode is selected, so the charge lights can change colour with the mode.
    /// Leave empty to keep the state from the gun's MagazineVisuals.
    /// </summary>
    [DataField]
    public string? MagState;
}

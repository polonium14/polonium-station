using Content.Shared._Polonium.Weapons.Ranged.Systems;

namespace Content.Shared._Polonium.Weapons.Ranged.Components;

/// <summary>
/// Widens the spread of a gun while its holder moves.
/// The penalty grows with the holder's speed and is added on top of the gun's recoil spread.
/// </summary>
[RegisterComponent]
[Access(typeof(GunMovementSpreadSystem))]
public sealed partial class GunMovementSpreadComponent : Component
{
    /// <summary>
    /// Extra spread at full walking speed.
    /// </summary>
    [DataField]
    public Angle Walk = Angle.FromDegrees(1);

    /// <summary>
    /// Extra spread at full running speed.
    /// </summary>
    [DataField]
    public Angle Run = Angle.FromDegrees(4);

    /// <summary>
    /// Extra spread while sprinting.
    /// </summary>
    [DataField]
    public Angle Sprint = Angle.FromDegrees(10);

    /// <summary>
    /// How long the sprint penalty takes to fade after the holder stops sprinting.
    /// </summary>
    [DataField]
    public TimeSpan SprintRecovery = TimeSpan.FromSeconds(1);
}

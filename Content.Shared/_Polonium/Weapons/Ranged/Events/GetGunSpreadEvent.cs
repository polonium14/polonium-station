namespace Content.Shared._Polonium.Weapons.Ranged.Events;

/// <summary>
/// Raised on a gun to find the spread cone of its next shot, after recoil was applied.
/// Handlers may widen or narrow <see cref="Spread"/>.
/// </summary>
/// <param name="User">Who holds the gun, if anyone.</param>
/// <param name="Spread">Full width of the cone; shots deviate up to half of it to either side.</param>
[ByRefEvent]
public record struct GetGunSpreadEvent(EntityUid? User, Angle Spread);

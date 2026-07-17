using System.Numerics;

namespace Content.Shared._RMC14.Weapons.Ranged;

[ByRefEvent]
public record struct RMCBeforeMuzzleFlashEvent(EntityUid Weapon, Vector2 Offset = default);

using System.Numerics;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Weapons.Ranged;

[ByRefEvent]
public record struct BeforeAttemptShootEvent(EntityCoordinates Origin, Vector2 Offset, bool Handled = false);

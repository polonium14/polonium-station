using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised to determine the prototype that will be shot from an ammo provider.
/// </summary>
[ByRefEvent]
public record struct CheckShootPrototypeEvent(EntityPrototype? ShootPrototype = null);

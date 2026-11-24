namespace Content.Server._Polonium.Delivery;

/// <summary>
/// Raised on a delivery right after a delivery spawner created it, once its recipient is already assigned.
/// </summary>
/// <param name="Spawner">The delivery spawner that created the delivery.</param>
[ByRefEvent]
public readonly record struct DeliverySpawnedEvent(EntityUid Spawner);

namespace Content.Shared._Polonium.Teleportation;

/// <summary>
/// Raised on an entity that went into a portal, right before the portal teleports it.
/// The client raises it even when it can't predict where the entity comes out.
/// </summary>
/// <param name="Portal">The portal the entity went into.</param>
[ByRefEvent]
public record struct PortalEnteredEvent(EntityUid Portal)
{
    /// <summary>
    /// Set when a handler already dealt with the entity and the portal should leave it alone.
    /// </summary>
    public bool Handled;
}

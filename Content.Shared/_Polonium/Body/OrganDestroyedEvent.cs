namespace Content.Shared._Polonium.Body;

/// <summary>
/// Raised on a body, on the server, after one of its organs was destroyed and taken out of the body.
/// </summary>
/// <param name="Organ">The destroyed organ. It is already queued for deletion.</param>
/// <param name="Parent">The body part the organ was a child of before it was removed.</param>
[ByRefEvent]
public readonly record struct OrganDestroyedEvent(EntityUid Organ, EntityUid? Parent);

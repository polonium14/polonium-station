namespace Content.Shared._Polonium.Modules.ModSuit.Events;

/// <summary>
/// Raised on the wearer after a part of their suit got sealed or unsealed and its sealed components were updated.
/// </summary>
[ByRefEvent]
public readonly record struct ModSuitWearerSealChangedEvent(EntityUid Part, bool Sealed);

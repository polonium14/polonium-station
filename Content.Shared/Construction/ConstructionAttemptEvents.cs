using Content.Shared.Construction.Prototypes;

namespace Content.Shared.Construction;

/// <summary>
/// Raised on a construction entity before a graph interaction or the deconstruct verb.
/// </summary>
[ByRefEvent]
public record struct ConstructionInteractAttemptEvent(EntityUid? User, bool ShowPopup = false)
{
    public bool Cancelled;
}

/// <summary>
/// Raised on the user before an item or structure recipe starts.
/// </summary>
[ByRefEvent]
public record struct ConstructionStartAttemptEvent(ConstructionPrototype Prototype)
{
    public bool Cancelled;
}

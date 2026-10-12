using Content.Shared.Research.Prototypes;

namespace Content.Shared._Polonium.Lathe;

[ByRefEvent]
public record struct LatheQueueAttemptEvent(EntityUid Actor, LatheRecipePrototype Recipe, int Quantity, bool Cancelled = false);

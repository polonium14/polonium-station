namespace Content.Shared._RMC14.Xenonids.Fortify;

[ByRefEvent]
public record struct XenoFortifyAttemptEvent
{
    public bool Cancelled;
}

[ByRefEvent]
public readonly record struct XenoFortifiedEvent(bool Fortified);

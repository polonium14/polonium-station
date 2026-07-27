namespace Content.Shared._RMC14.Xenonids.Rest;

[ByRefEvent]
public record struct XenoRestAttemptEvent
{
    public bool Cancelled;
}

[ByRefEvent]
public readonly record struct XenoRestEvent(bool Resting);

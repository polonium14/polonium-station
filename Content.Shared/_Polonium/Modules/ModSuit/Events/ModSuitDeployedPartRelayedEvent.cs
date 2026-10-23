namespace Content.Shared._Polonium.Modules.ModSuit.Events;

[ByRefEvent]
public struct ModSuitDeployedPartRelayedEvent<T>(T args, EntityUid part)
{
    public T Args = args;
    public readonly EntityUid Part = part;
}

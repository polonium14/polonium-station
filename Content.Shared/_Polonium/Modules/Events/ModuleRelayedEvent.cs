namespace Content.Shared._Polonium.Modules.Events;

[ByRefEvent]
public struct ModuleRelayedEvent<T>(T args, EntityUid moduleOwner)
{
    public T Args = args;
    public readonly EntityUid ModuleOwner = moduleOwner;
}

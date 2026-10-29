namespace Content.Shared._Polonium.Modules.Events;

[ByRefEvent]
public struct ModuleContainerRelayedEvent<T>(T args, EntityUid module)
{
    public T Args = args;
    public readonly EntityUid Module = module;
}

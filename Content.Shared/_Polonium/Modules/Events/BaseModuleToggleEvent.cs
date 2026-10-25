namespace Content.Shared._Polonium.Modules.Events;

public abstract partial class BaseModuleToggleEvent(EntityUid container, EntityUid? user) : EntityEventArgs
{
    public readonly EntityUid Container = container;
    public readonly EntityUid? User = user;
}

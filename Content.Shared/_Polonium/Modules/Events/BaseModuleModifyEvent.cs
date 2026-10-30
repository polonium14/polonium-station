namespace Content.Shared._Polonium.Modules.Events;

public abstract partial class BaseModuleModifyEvent(EntityUid container) : EntityEventArgs
{
    public readonly EntityUid Container = container;
}

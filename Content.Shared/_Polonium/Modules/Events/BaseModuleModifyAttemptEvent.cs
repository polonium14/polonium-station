namespace Content.Shared._Polonium.Modules.Events;

public abstract partial class BaseModuleModifyAttemptEvent(EntityUid container) : CancellableEntityEventArgs
{
    public readonly EntityUid Container = container;
    public LocId? Reason;
}

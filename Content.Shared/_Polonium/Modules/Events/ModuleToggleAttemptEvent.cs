namespace Content.Shared._Polonium.Modules.Events;

public sealed partial class ModuleToggleAttemptEvent(bool toggle, EntityUid container, EntityUid? user) : CancellableEntityEventArgs
{
    public readonly EntityUid Container = container;
    public readonly EntityUid? User = user;
    public readonly bool Toggle = toggle;
    public LocId? Reason = "module-toggle-failed-generic";
}

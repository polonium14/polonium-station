namespace Content.Shared._Polonium.Modules.Events;

public sealed partial class ModuleGetUserEvent : EntityEventArgs
{
    public EntityUid? User;
}

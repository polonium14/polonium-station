namespace Content.Shared._Polonium.Modules.Events;

public sealed partial class ModuleEnabledEvent(EntityUid container, EntityUid? user) : BaseModuleToggleEvent(container, user);

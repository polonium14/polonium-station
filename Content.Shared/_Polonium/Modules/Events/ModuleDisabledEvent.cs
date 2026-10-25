namespace Content.Shared._Polonium.Modules.Events;

public sealed partial class ModuleDisabledEvent(EntityUid container, EntityUid? user) : BaseModuleToggleEvent(container, user);

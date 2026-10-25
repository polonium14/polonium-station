namespace Content.Shared._Polonium.Modules.Events;

public sealed partial class ModuleRemovedContainerEvent(EntityUid container) : BaseModuleModifyEvent(container);

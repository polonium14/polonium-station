namespace Content.Shared._Polonium.Modules.Events;

public sealed partial class ModuleAddedContainerEvent(EntityUid container) : BaseModuleModifyEvent(container);

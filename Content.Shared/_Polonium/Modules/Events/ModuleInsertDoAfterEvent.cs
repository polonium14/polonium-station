using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Modules.Events;

[Serializable, NetSerializable]
public sealed partial class ModuleInsertDoAfterEvent : SimpleDoAfterEvent;

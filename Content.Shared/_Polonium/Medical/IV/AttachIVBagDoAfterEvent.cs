using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Medical.IV;

[Serializable, NetSerializable]
public sealed partial class AttachIVBagDoAfterEvent : SimpleDoAfterEvent;

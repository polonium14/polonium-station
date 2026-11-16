using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Medical.Cpr;

/// <summary>
/// One cycle of chest compressions.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CprDoAfterEvent : SimpleDoAfterEvent;

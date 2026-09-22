using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Vehicles;

[Serializable, NetSerializable]
public sealed partial class TankRepairDoAfterEvent : SimpleDoAfterEvent
{
}
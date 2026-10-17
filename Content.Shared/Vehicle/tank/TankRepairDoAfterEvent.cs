using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Vehicles;

[Serializable, NetSerializable]
public sealed partial class TankRepairDoAfterEvent : SimpleDoAfterEvent
{
    // 0 = panele (śrubokręt), 1 = pancerz (spawarka), 2 = kadłub (klucz)
    public int RepairMode;

    public TankRepairDoAfterEvent(int mode)
    {
        RepairMode = mode;
    }

    public TankRepairDoAfterEvent()
    {
    }
}
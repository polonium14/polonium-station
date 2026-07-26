using System.Linq;
using Content.Shared.DeviceNetwork.Components;

namespace Content.Shared.DeviceNetwork.Systems;

public abstract class SharedDeviceListSystem : EntitySystem
{
    public IEnumerable<EntityUid> GetAllDevices(EntityUid uid, DeviceListComponent? component = null)
    {
        if (!Resolve(uid, ref component))
        {
            return new EntityUid[] { };
        }
        return component.Devices;
    }

    /// <summary>
    /// Clean deleted or invalid device uids.
    /// </summary>
    protected bool PruneDeletedDevices(EntityUid uid, DeviceListComponent list)
    {
        var temp = list.Devices.ToList();

        // if we did not remove any devices
        if (list.Devices.RemoveWhere(d => TerminatingOrDeleted(d)) == 0)
            return false;

        RaiseLocalEvent(uid, new DeviceListUpdateEvent(temp, list.Devices.ToList()));

        Dirty(uid, list);

        return true;
    }
}

public sealed class DeviceListUpdateEvent : EntityEventArgs
{
    public DeviceListUpdateEvent(List<EntityUid> oldDevices, List<EntityUid> devices)
    {
        OldDevices = oldDevices;
        Devices = devices;
    }

    public List<EntityUid> OldDevices { get; }
    public List<EntityUid> Devices { get; }
}

public enum DeviceListUpdateResult : byte
{
    NoComponent,
    TooManyDevices,
    UpdateOk
}

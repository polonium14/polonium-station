using System.Runtime.InteropServices;
using Content.Shared.DeviceConfigurator.Components;
using Content.Shared.DeviceConfigurator.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.DeviceNetwork;

/// <summary>
/// Removes deleted devices that stayed in device lists because their shutdown never reached the list.
/// </summary>
public sealed partial class DeviceListPruneSystem : EntitySystem
{
    [Dependency] private DeviceListSystem _deviceList = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan PruneInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextPrune;
    private readonly List<EntityUid> _alive = new();

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextPrune)
            return;

        _nextPrune = _timing.CurTime + PruneInterval;

        var query = EntityQueryEnumerator<DeviceListComponent>();
        while (query.MoveNext(out var uid, out var list))
        {
            _alive.Clear();
            var stale = false;
            foreach (var device in _deviceList.GetAllDevices((uid, list)))
            {
                if (TerminatingOrDeleted(device))
                    stale = true;
                else
                    _alive.Add(device);
            }

            if (stale)
                _deviceList.UpdateDeviceList((uid, list), CollectionsMarshal.AsSpan(_alive));
        }
    }
}

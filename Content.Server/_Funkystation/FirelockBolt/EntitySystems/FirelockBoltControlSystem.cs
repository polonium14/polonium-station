using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Monitor.Systems;
using Content.Server.Doors.Systems;
using Content.Server.Power.EntitySystems;
using Content.Shared._Funkystation.FirelockBolt.Components;
using Content.Shared._Funkystation.FirelockBolt.EntitySystems;
using Content.Shared.Atmos.Monitor;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Doors.Components;

namespace Content.Server._Funkystation.FirelockBolt.EntitySystems;

public sealed partial class FirelockBoltControlSystem : SharedFirelockBoltControlSystem
{
    [Dependency] private AtmosAlarmableSystem _atmosAlarmable = default!;
    [Dependency] private EntityQuery<FirelockBoltControlComponent> _boltControlQuery = default!;
    [Dependency] private EntityQuery<AtmosAlarmableComponent> _alarmableQuery = default!;
    [Dependency] private EntityQuery<DeviceListComponent> _deviceListQuery = default!;
    [Dependency] private EntityQuery<DeviceNetworkComponent> _deviceNetQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        // network packet path on the firelock itself
        SubscribeLocalEvent<FirelockBoltControlComponent, AtmosAlarmEvent>(OnAtmosAlarm, before: new[] { typeof(FirelockSystem) });
        SubscribeLocalEvent<FireAlarmComponent, AtmosAlarmEvent>(OnFireAlarm, before: new[] { typeof(FirelockSystem) });
    }

    private void OnAtmosAlarm(EntityUid uid, FirelockBoltControlComponent component, AtmosAlarmEvent args)
    {
        ApplyAlarmState((uid, component), args.AlarmType);
    }

    private void OnFireAlarm(EntityUid uid, FireAlarmComponent component, AtmosAlarmEvent args)
    {
        PushAlarmToDeviceList(uid, args.AlarmType);
    }

    /// <summary>
    /// Air/FireAlarm status change - update DeviceList firelocks without waiting for net packets
    /// </summary>
    public void PushAlarmToDeviceList(EntityUid alarmUid, AtmosAlarmType alarmType)
    {
        if (!this.IsPowered(alarmUid, EntityManager))
            return;

        if (!_deviceListQuery.TryComp(alarmUid, out var deviceList))
            return;

        string? alarmAddress = null;
        if (_deviceNetQuery.TryComp(alarmUid, out var alarmNet)
            && !string.IsNullOrEmpty(alarmNet.Address))
        {
            alarmAddress = alarmNet.Address;
        }

        foreach (var device in deviceList.Devices)
        {
            if (!_boltControlQuery.HasComp(device))
                continue;

            if (_alarmableQuery.TryComp(device, out var alarmable) && alarmAddress != null)
            {
                alarmable.NetworkAlarmStates[alarmAddress] = alarmType;

                var netMax = _atmosAlarmable.TryGetHighestAlert(device, out var highest, alarmable)
                    ? highest.Value
                    : AtmosAlarmType.Normal;

                if (alarmable.LastAlarmState != netMax)
                {
                    alarmable.LastAlarmState = netMax;
                    RaiseLocalEvent(device, new AtmosAlarmEvent(netMax), true);
                    continue;
                }

                if (_boltControlQuery.TryComp(device, out var boltControl))
                    ApplyAlarmState((device, boltControl), netMax);

                continue;
            }

            if (_boltControlQuery.TryComp(device, out var boltOnly))
                ApplyAlarmState((device, boltOnly), alarmType);
        }
    }

    private void ApplyAlarmState(Entity<FirelockBoltControlComponent> ent, AtmosAlarmType alarmType)
    {
        var alarmActive = alarmType == AtmosAlarmType.Danger;
        if (ent.Comp.AlarmActive != alarmActive)
        {
            ent.Comp.AlarmActive = alarmActive;
            Dirty(ent, ent.Comp);
        }

        if (!ent.Comp.Override)
            UpdateHazardBolts(ent);

        PushState(ent);
    }

    public void RefreshAlarmBolts(Entity<FirelockBoltControlComponent> ent)
    {
        var alarmType = AtmosAlarmType.Normal;
        if (_alarmableQuery.TryComp(ent.Owner, out var alarmable)
            && alarmable.LastAlarmState != AtmosAlarmType.Invalid)
        {
            alarmType = alarmable.LastAlarmState;
        }

        ApplyAlarmState(ent, alarmType);
    }
}

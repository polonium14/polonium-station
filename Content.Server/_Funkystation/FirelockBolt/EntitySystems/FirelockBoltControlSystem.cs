// SPDX-FileCopyrightText: 2026 MaiaArai <158123176+YaraaraY@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.Monitor.Systems;
using Content.Server.Doors.Systems;
using Content.Shared._Funkystation.FirelockBolt.Components;
using Content.Shared._Funkystation.FirelockBolt.EntitySystems;
using Content.Shared.Atmos.Monitor;

namespace Content.Server._Funkystation.FirelockBolt.EntitySystems;

public sealed partial class FirelockBoltControlSystem : SharedFirelockBoltControlSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FirelockBoltControlComponent, AtmosAlarmEvent>(OnAtmosAlarm, before: new[] { typeof(FirelockSystem) });
    }

    private void OnAtmosAlarm(EntityUid uid, FirelockBoltControlComponent component, AtmosAlarmEvent args)
    {
        component.AlarmActive = args.AlarmType == AtmosAlarmType.Danger;
        Dirty(uid, component);

        // bolt/unbolt immediately when air alarm (or fire alarm) status changes
        if (!component.Override)
            UpdateHazardBolts((uid, component));

        PushState((uid, component));
    }
}

// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;
using Content.Server.Station.Systems;

namespace Content.Server._Polonium.StationEvents;

/// <summary>
/// Like BreakerFlip, but disables every powered APC on the station, with no announcement.
/// </summary>
[UsedImplicitly]
public sealed partial class BreakerDownRule : StationEventSystem<BreakerDownRuleComponent>
{
    [Dependency] private ApcSystem _apcSystem = default!;

    protected override void Started(
        EntityUid uid,
        BreakerDownRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!Station.TryGetRandomStation(out var chosenStation))
            return;

        foreach (var (apcUid, apc) in Station.GetEntitiesWithComponentOnStation<ApcComponent>(chosenStation.Value, true))
        {
            if (!apc.MainBreakerEnabled)
                continue;

            _apcSystem.ApcToggleBreaker(apcUid, apc);
        }
    }
}

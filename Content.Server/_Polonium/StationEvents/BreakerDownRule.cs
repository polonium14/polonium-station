using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using JetBrains.Annotations;

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

        if (!TryGetRandomStation(out var chosenStation))
            return;

        var query = EntityQueryEnumerator<ApcComponent, TransformComponent>();
        while (query.MoveNext(out var apcUid, out var apc, out var xform))
        {
            if (!apc.MainBreakerEnabled)
                continue;

            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != chosenStation)
                continue;

            _apcSystem.ApcToggleBreaker(apcUid, apc);
        }
    }
}

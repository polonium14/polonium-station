using Content.Server._Polonium.StationAi;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;

namespace Content.Server._Polonium.StationEvents;

/// <summary>
/// Admin GameRule: announces and bolts all external airlocks on a random eligible station.
/// </summary>
public sealed partial class ExternalLockdownRule : StationEventSystem<ExternalLockdownRuleComponent>
{
    [Dependency] private ExternalLockdownSystem _lockdown = default!;

    protected override void Started(
        EntityUid uid,
        ExternalLockdownRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station))
            return;

        _lockdown.BoltExternalAirlocksOnStation(station.Value);
    }
}

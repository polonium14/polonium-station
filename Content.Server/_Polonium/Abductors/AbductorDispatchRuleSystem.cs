using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Robust.Server.Player;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorDispatchRuleSystem : GameRuleSystem<AbductorDispatchRuleComponent>
{
    [Dependency] private IPlayerManager _player = default!;

    protected override void Started(Entity<AbductorDispatchRuleComponent, GameRuleComponent> rule, ref GameRuleStartedEvent args)
    {
        base.Started(rule, ref args);

        var dispatch = rule.Comp1;
        GameTicker.StartGameRule(_player.PlayerCount >= dispatch.PairMinimumPlayers ? dispatch.Pair : dispatch.Lone);
    }

    protected override void ActiveTick(EntityUid uid, AbductorDispatchRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        ForceEndSelf((uid, gameRule));
    }
}

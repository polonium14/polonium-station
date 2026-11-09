using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.GameTicking;

public sealed class FinishedGameRuleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRuleEndedEvent>(OnRuleEnded);
    }

    private void OnRuleEnded(ref GameRuleEndedEvent args)
    {
        if (TerminatingOrDeleted(args.Rule) || !TryComp<GameRuleComponent>(args.Rule, out var rule))
            return;

        EnsureComp<FinishedGameRuleComponent>(args.Rule).ActivatedAt = rule.ActivatedAt;
    }

    /// <summary>
    /// Counts the ended runs of a game rule this round.
    /// </summary>
    /// <param name="rule">Prototype of the game rule.</param>
    /// <param name="lastActivation">Round time the latest ended run was activated at.</param>
    /// <returns>Number of ended runs this round.</returns>
    public int GetEndedOccurrences(EntProtoId rule, out TimeSpan lastActivation)
    {
        var count = 0;
        lastActivation = TimeSpan.Zero;

        var query = EntityQueryEnumerator<FinishedGameRuleComponent, MetaDataComponent>();
        while (query.MoveNext(out var finished, out var meta))
        {
            if (meta.EntityPrototype?.ID != rule.Id)
                continue;

            count++;
            if (lastActivation < finished.ActivatedAt)
                lastActivation = finished.ActivatedAt;
        }

        return count;
    }
}

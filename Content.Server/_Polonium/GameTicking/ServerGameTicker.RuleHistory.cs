using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking;

public sealed partial class ServerGameTicker
{
    /// <summary>
    /// Counts the ended runs of a game rule this round. Ended rules lose their <see cref="GameRuleComponent"/>,
    /// so queries over that component do not see them anymore.
    /// </summary>
    /// <param name="rule">Prototype of the game rule.</param>
    /// <param name="lastStart">Round time the latest ended run started at.</param>
    /// <returns>Number of ended runs this round.</returns>
    public int GetEndedOccurrences(EntProtoId rule, out TimeSpan lastStart)
    {
        var count = 0;
        lastStart = TimeSpan.Zero;

        foreach (var (time, uid, stage) in AllRoundGameRules)
        {
            if (stage != GameRuleLifeStage.Ended
                || !TryComp(uid, out MetaDataComponent? meta)
                || meta.EntityPrototype?.ID != rule.Id)
            {
                continue;
            }

            count++;
            if (lastStart < time)
                lastStart = time;
        }

        return count;
    }
}

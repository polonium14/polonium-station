using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Name;
using Content.Shared._RMC14.Xenonids.Rank;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Xenonids.Rank;

public sealed class XenoRankSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly ISharedPlaytimeManager _playtime = default!;

    private TimeSpan _rankTwoTime;
    private TimeSpan _rankThreeTime;
    private TimeSpan _rankFourTime;
    private TimeSpan _rankFiveTime;
    private TimeSpan _rankSixTime;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoComponent, MindAddedMessage>(OnXenoMindAdded);
        SubscribeLocalEvent<XenoRankComponent, RefreshNameModifiersEvent>(OnRankRefreshName, before: [typeof(SharedXenoNameSystem)]);

        Subs.CVar(_config, RMCCVars.RMCPlaytimeBronzeMedalTimeHours, v => _rankTwoTime = TimeSpan.FromHours(v), true);
        Subs.CVar(_config, RMCCVars.RMCPlaytimeSilverMedalTimeHours, v => _rankThreeTime = TimeSpan.FromHours(v), true);
        Subs.CVar(_config, RMCCVars.RMCPlaytimeGoldMedalTimeHours, v => _rankFourTime = TimeSpan.FromHours(v), true);
        Subs.CVar(_config, RMCCVars.RMCPlaytimePlatinumMedalTimeHours, v => _rankFiveTime = TimeSpan.FromHours(v), true);
        Subs.CVar(_config, RMCCVars.RMCPlaytimeRubyMedalTimeHours, v => _rankSixTime = TimeSpan.FromHours(v), true);
    }

    private void OnXenoMindAdded(Entity<XenoComponent> xeno, ref MindAddedMessage args)
    {
        if (!TryComp(xeno, out ActorComponent? actor))
            return;

        UpdateRank(xeno, actor.PlayerSession);
    }

    private void OnRankRefreshName(Entity<XenoRankComponent> ent, ref RefreshNameModifiersEvent args)
    {
        if (!TryComp<XenoRankNamesComponent>(ent, out var rankNamesComp))
            return;

        if (!rankNamesComp.RankNames.TryGetValue(ent.Comp.Rank, out var rank))
            return;

        args.AddModifier(rank);
    }

    private void UpdateRank(EntityUid xeno, ICommonSession player)
    {
        if (!HasComp<XenoComponent>(xeno))
            return;

        var time = GetXenoPlaytime(player);

        int rank;
        try
        {
            if (time > _rankSixTime)
                rank = 6;
            else if (time > _rankFiveTime)
                rank = 5;
            else if (time > _rankFourTime)
                rank = 4;
            else if (time > _rankThreeTime)
                rank = 3;
            else if (time > _rankTwoTime)
                rank = 2;
            else
                rank = 0;
        }
        catch
        {
            rank = 0;
        }

        var rankComp = EnsureComp<XenoRankComponent>(xeno);
        if (rankComp.Rank == rank)
            return;

        rankComp.Rank = rank;
        Dirty(xeno, rankComp);
        _nameModifier.RefreshNameModifiers(xeno);
    }

    private TimeSpan GetXenoPlaytime(ICommonSession player)
    {
        var total = TimeSpan.Zero;
        try
        {
            foreach (var (_, time) in _playtime.GetPlayTimes(player))
                total += time;
        }
        catch (Exception e)
        {
            Log.Error($"Error reading xeno playtime for rank:\n{e}");
        }

        return total;
    }
}

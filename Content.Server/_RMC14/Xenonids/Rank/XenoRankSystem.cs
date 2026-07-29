using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Rank;
using Content.Shared.FixedPoint;

namespace Content.Server._RMC14.Xenonids.Rank;

public sealed partial class XenoRankSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<XenoComponent, AfterNewXenoEvolvedEvent>(OnAfterEvolved);
    }

    private void OnAfterEvolved(Entity<XenoComponent> ent, ref AfterNewXenoEvolvedEvent args)
    {
        UpdateRank(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<XenoEvolutionComponent, XenoComponent>();
        while (query.MoveNext(out var uid, out _, out _))
            UpdateRank(uid);
    }

    private void UpdateRank(EntityUid xeno)
    {
        if (!HasComp<XenoComponent>(xeno))
            return;

        var rank = GetEvolutionRank(xeno);
        var rankComp = EnsureComp<XenoRankComponent>(xeno);
        if (rankComp.Rank == rank)
            return;

        rankComp.Rank = rank;
        Dirty(xeno, rankComp);
    }

    // same chevron steps as before - just progress to Max instead of playtime
    private int GetEvolutionRank(EntityUid xeno)
    {
        if (!TryComp(xeno, out XenoEvolutionComponent? evolution) || evolution.Max <= FixedPoint2.Zero)
            return 0;

        var ratio = (evolution.Points / evolution.Max).Float();

        if (ratio >= 1f)
            return 6;
        if (ratio > 0.99f)
            return 5;
        if (ratio > 0.6f)
            return 4;
        if (ratio > 0.4f)
            return 3;
        if (ratio > 0.2f)
            return 2;

        return 0;
    }
}

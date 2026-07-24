using Content.Shared.Bed.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Shitmed.Medical.Surgery;

public sealed partial class UnfinishedSurgeryPenaltySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedSurgerySystem _surgery = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(3);

    private const float BleedPerUnfinishedOrgan = 2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, UnbuckledEvent>(OnUnbuckled);
        SubscribeLocalEvent<UnfinishedSurgeryPenaltyComponent, BleedModifierEvent>(OnBleedModifier);
    }

    private void OnUnbuckled(Entity<BodyComponent> ent, ref UnbuckledEvent args)
    {
        if (!HasComp<HealOnBuckleComponent>(args.Strap))
            return;

        var unfinishedCount = CountUnfinishedOrgans(ent.Comp);
        if (unfinishedCount == 0)
            return;

        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream))
            return;

        var before = bloodstream.BleedAmountNotFromWounds;
        _bloodstream.TryModifyBleedAmount((ent.Owner, bloodstream), BleedPerUnfinishedOrgan * unfinishedCount);
        var applied = bloodstream.BleedAmountNotFromWounds - before;

        var isNew = !HasComp<UnfinishedSurgeryPenaltyComponent>(ent);
        var penalty = EnsureComp<UnfinishedSurgeryPenaltyComponent>(ent);
        if (isNew)
            penalty.NextTick = _timing.CurTime + TickInterval;
        penalty.PenaltyBleed += applied;
        Dirty(ent.Owner, penalty);
    }

    private void OnBleedModifier(Entity<UnfinishedSurgeryPenaltyComponent> ent, ref BleedModifierEvent args)
    {
        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream))
            return;

        if (ent.Comp.PenaltyBleed > bloodstream.BleedAmountNotFromWounds)
        {
            ent.Comp.PenaltyBleed = bloodstream.BleedAmountNotFromWounds;
            Dirty(ent);
        }

        args.BleedReductionAmount = Math.Min(args.BleedReductionAmount,
            Math.Max(0f, bloodstream.BleedAmountNotFromWounds - ent.Comp.PenaltyBleed));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<UnfinishedSurgeryPenaltyComponent, BodyComponent>();
        while (query.MoveNext(out var uid, out var penalty, out var body))
        {
            if (_timing.CurTime < penalty.NextTick)
                continue;

            penalty.NextTick += TickInterval;


            if (TryComp<BloodstreamComponent>(uid, out var bloodstream)
                && penalty.PenaltyBleed > bloodstream.BleedAmountNotFromWounds)
            {
                penalty.PenaltyBleed = bloodstream.BleedAmountNotFromWounds;
                Dirty(uid, penalty);
            }

            if (CountUnfinishedOrgans(body) != 0)
                continue;

            if (bloodstream != null)
                _bloodstream.TryModifyBleedAmount((uid, bloodstream), -penalty.PenaltyBleed);

            RemComp<UnfinishedSurgeryPenaltyComponent>(uid);
        }
    }

    private int CountUnfinishedOrgans(BodyComponent body)
    {
        if (body.Organs is null)
            return 0;

        var count = 0;
        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (_surgery.HasUnfinishedSurgerySteps(organ))
                count++;
        }

        return count;
    }
}

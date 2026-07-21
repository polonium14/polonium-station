using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Bed.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Robust.Shared.Timing;

namespace Content.Server._Shitmed.Medical.Surgery;

/// <summary>
/// Penalizes getting up (unbuckling from a bed/operating table) while surgery is still
/// unfinished (see SharedSurgerySystem.HasUnfinishedSurgerySteps) - the patient slowly starts to
/// bleed, using the same BleedAmountNotFromWounds/TryModifyBleedAmount pool vanilla mobs without
/// wound support already bleed through (see SharedBloodstreamSystem.cs), rather than a raw
/// damage tick or a synthetic Wound entity: TryModifyBleedAmount isn't gated behind
/// HasWoundSupport (only the automatic damage-triggered call in OnDamageChanged is), so it's
/// safe to call directly here, and it gets the real Bleeding HUD alert + eventual floor puddle
/// (SharedBloodstreamSystem.TickBleed/TryBleedOut) entirely for free.
///
/// Gated on HealOnBuckleComponent (the medbay-bed marker) rather than the surgery-specific
/// OperatingTableComponent - that component is currently unattached to any prototype (a
/// separately-known, out-of-scope gap), whereas OperatingTable's own prototype has
/// `parent: Bed` and Bed already carries HealOnBuckle, so this one check covers both real beds
/// and the operating table without reviving that dead gate. Plain chairs/toilets (bare
/// StrapComponent, no HealOnBuckle) are correctly excluded.
/// </summary>
public sealed partial class UnfinishedSurgeryPenaltySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSurgerySystem _surgery = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Relative to vanilla's own BleedReductionAmount (0.33/tick clotting) - one unfinished
    /// organ nets a slow climb (+0.07/tick), crossing into visibly bleeding within a tick but
    /// only reaching a serious bleed after several minutes of being ignored. Scales per
    /// unfinished organ, so sloppier half-finished surgery bleeds faster.
    /// </summary>
    private const float TopUpPerUnfinishedOrgan = 0.4f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BuckleComponent, UnbuckledEvent>(OnUnbuckled);
    }

    private void OnUnbuckled(Entity<BuckleComponent> ent, ref UnbuckledEvent args)
    {
        if (!HasComp<HealOnBuckleComponent>(args.Strap))
            return;

        if (!TryComp<BodyComponent>(ent, out var body) || CountUnfinishedOrgans(body) == 0)
            return;

        if (!HasComp<UnfinishedSurgeryPenaltyComponent>(ent))
        {
            var penalty = EnsureComp<UnfinishedSurgeryPenaltyComponent>(ent);
            penalty.NextTick = _timing.CurTime + TickInterval;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<UnfinishedSurgeryPenaltyComponent, BodyComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var penalty, out var body, out var bloodstream))
        {
            if (_timing.CurTime < penalty.NextTick)
                continue;

            penalty.NextTick += TickInterval;

            var unfinishedCount = CountUnfinishedOrgans(body);
            if (unfinishedCount == 0)
            {
                RemComp<UnfinishedSurgeryPenaltyComponent>(uid);
                continue;
            }

            _bloodstream.TryModifyBleedAmount((uid, bloodstream), TopUpPerUnfinishedOrgan * unfinishedCount);
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

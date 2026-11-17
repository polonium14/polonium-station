// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Rejuvenate;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Shitmed.Medical.Surgery;

/// <summary>
/// An open surgical incision bleeds for as long as it stays open: strongly until its bleeders are clamped,
/// weakly afterwards. The bleeding does not clot and stops when the incision is closed.
/// </summary>
public sealed partial class UnfinishedSurgeryPenaltySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(3);

    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";
    private static readonly ProtoId<OrganCategoryPrototype> Head = "Head";

    /// <summary>
    /// Share of the torso's bleeding an incision in the head bleeds, and one in an arm or a leg.
    /// </summary>
    private const float HeadShare = 0.5f;
    private const float LimbShare = 0.25f;

    private TimeSpan _nextTick;
    private readonly Dictionary<EntityUid, float> _bleeding = [];
    private readonly List<EntityUid> _closed = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnfinishedSurgeryPenaltyComponent, BleedModifierEvent>(OnBleedModifier);
        SubscribeLocalEvent<UnfinishedSurgeryPenaltyComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<UnfinishedSurgeryPenaltyComponent> ent, ref RejuvenateEvent args)
    {
        // BloodstreamSystem clears the bleeding itself. Drop the tracking immediately so
        // future bleeding cannot inherit the old surgery penalty's protection from clotting.
        RemComp<UnfinishedSurgeryPenaltyComponent>(ent);
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

        if (!_net.IsServer || _timing.CurTime < _nextTick)
            return;

        _nextTick = _timing.CurTime + TickInterval;

        var open = _cfg.GetCVar(CCVars.SurgeryOpenIncisionBleed);
        var clamped = _cfg.GetCVar(CCVars.SurgeryClampedIncisionBleed);

        _bleeding.Clear();
        var incisions = EntityQueryEnumerator<IncisionOpenComponent, OrganComponent>();
        while (incisions.MoveNext(out var part, out _, out var organ))
        {
            if (organ.Body is not { } body)
                continue;

            var share = organ.Category == Torso ? 1f : organ.Category == Head ? HeadShare : LimbShare;
            var bleed = (HasComp<BleedersClampedComponent>(part) ? clamped : open) * share;
            _bleeding[body] = _bleeding.GetValueOrDefault(body) + bleed;
        }

        foreach (var (body, bleed) in _bleeding)
        {
            SetBleed(body, bleed);
        }

        _closed.Clear();
        var penalties = EntityQueryEnumerator<UnfinishedSurgeryPenaltyComponent>();
        while (penalties.MoveNext(out var uid, out _))
        {
            if (!_bleeding.ContainsKey(uid))
                _closed.Add(uid);
        }

        foreach (var uid in _closed)
        {
            SetBleed(uid, 0f);
            RemComp<UnfinishedSurgeryPenaltyComponent>(uid);
        }
    }

    /// <summary>
    /// Brings the surgical share of the body's bleeding to the wanted amount.
    /// </summary>
    private void SetBleed(EntityUid body, float wanted)
    {
        if (!TryComp<BloodstreamComponent>(body, out var bloodstream))
            return;

        var penalty = EnsureComp<UnfinishedSurgeryPenaltyComponent>(body);
        var held = Math.Min(penalty.PenaltyBleed, bloodstream.BleedAmountNotFromWounds);
        var before = bloodstream.BleedAmountNotFromWounds;
        _bloodstream.TryModifyBleedAmount((body, bloodstream), wanted - held);

        // The bloodstream caps the total, so only what was really added or removed is tracked.
        penalty.PenaltyBleed = held + bloodstream.BleedAmountNotFromWounds - before;
        Dirty(body, penalty);
    }
}

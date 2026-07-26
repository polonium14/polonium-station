// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.FixedPoint;

namespace Content.Shared._Shitmed.Medical.Surgery.Pain.Systems;

public partial class PainSystem
{
    private void InitAffliction()
    {
        // Pain management hooks.
        SubscribeLocalEvent<PainInflicterComponent, WoundRemovedEvent>(OnPainRemoved);
        SubscribeLocalEvent<PainInflicterComponent, WoundSeverityPointChangedEvent>(OnPainChanged);
        SubscribeLocalEvent<WoundableComponent, TraumaBeingRemovedEvent>(OnTraumaBeingRemoved);
    }

    private const string PainModifierIdentifier = "WoundPain";
    private const string PainTraumaticModifierIdentifier = "TraumaticPain";
    private const string PainAdrenalineIdentifier = "PainAdrenaline";

    #region Event Handling

    private void OnPainChanged(Entity<PainInflicterComponent> woundEnt, ref WoundSeverityPointChangedEvent args)
    {
        if (!TryComp<OrganComponent>(args.Component.HoldingWoundable, out var organ)
            || organ.Body is not { } body
            || !_consciousness.TryGetNerveSystem(body, out var nerveSys))
            return;

        // bro how
        woundEnt.Comp.RawPain = args.NewSeverity;
        var woundPain = FixedPoint2.Zero;
        var traumaticPain = FixedPoint2.Zero;

        // Seed with this wound's own updated contribution before scanning the rest: on
        // creation, WoundSystem.Queries.TryCreateWound calls SetWoundSeverity (which raises
        // this event) before AddWound inserts the wound into the woundable's Wounds
        // container, so GetWoundableWounds below won't see it yet. Skip it in the loop to
        // avoid double-counting once it *is* in the container (every change after creation).
        switch (woundEnt.Comp.PainType)
        {
            case PainDamageTypes.TraumaticPain:
                traumaticPain += woundEnt.Comp.Pain;
                break;
            default:
                woundPain += woundEnt.Comp.Pain;
                break;
        }

        foreach (var (woundId, _) in _wound.GetWoundableWounds(args.Component.HoldingWoundable))
        {
            if (woundId == woundEnt.Owner)
                continue;

            if (!TryComp<PainInflicterComponent>(woundId, out var painInflicter))
                continue;

            switch (painInflicter.PainType)
            {
                // In case more Pain Types is added for some reasonm
                case PainDamageTypes.WoundPain:
                    woundPain += painInflicter.Pain;
                    break;
                case PainDamageTypes.TraumaticPain:
                    traumaticPain += painInflicter.Pain;
                    break;
                default:
                    woundPain += painInflicter.Pain;
                    break;
            }
        }

        if (!TryAddPainModifier(nerveSys.Value, args.Component.HoldingWoundable, PainModifierIdentifier, woundPain))
            TryChangePainModifier(nerveSys.Value, args.Component.HoldingWoundable, PainModifierIdentifier, woundPain);

        if (traumaticPain > 0)
        {
            if (!TryAddPainModifier(
                    nerveSys.Value,
                    args.Component.HoldingWoundable,
                    PainTraumaticModifierIdentifier,
                    traumaticPain,
                    PainDamageTypes.TraumaticPain))
            {
                TryChangePainModifier(
                    nerveSys.Value,
                    args.Component.HoldingWoundable,
                    PainTraumaticModifierIdentifier,
                    traumaticPain);
            }
        }
    }

    private void OnPainRemoved(Entity<PainInflicterComponent> woundEnt, ref WoundRemovedEvent args)
    {
        if (!TryComp<OrganComponent>(args.Component.HoldingWoundable, out var organ)
            || organ.Body is not { } body
            || !_consciousness.TryGetNerveSystem(body, out var nerveSys))
            return;

        // bro how
        woundEnt.Comp.RawPain = 0;
        var woundPain = FixedPoint2.Zero;
        var traumaticPain = FixedPoint2.Zero;
        foreach (var (woundId, _) in _wound.GetWoundableWounds(args.Component.HoldingWoundable))
        {
            if (!TryComp<PainInflicterComponent>(woundId, out var painInflicter))
                continue;

            switch (painInflicter.PainType)
            {
                // In case more Pain Types is added for some reasonm
                case PainDamageTypes.WoundPain:
                    woundPain += painInflicter.Pain;
                    break;
                case PainDamageTypes.TraumaticPain:
                    traumaticPain += painInflicter.Pain;
                    break;
                default:
                    woundPain += painInflicter.Pain;
                    break;
            }
        }

        if (woundPain <= 0)
            TryRemovePainModifier(nerveSys.Value, args.Component.HoldingWoundable, PainModifierIdentifier);
        else
            TryChangePainModifier(nerveSys.Value, args.Component.HoldingWoundable, PainModifierIdentifier, woundPain);

        if (traumaticPain <= 0)
            TryRemovePainModifier(nerveSys.Value, args.Component.HoldingWoundable, PainTraumaticModifierIdentifier);
        else
            TryChangePainModifier(nerveSys.Value, args.Component.HoldingWoundable, PainTraumaticModifierIdentifier, traumaticPain);
    }

    private void OnTraumaBeingRemoved(Entity<WoundableComponent> woundable, ref TraumaBeingRemovedEvent args)
    {
        if (args.TraumaType != TraumaType.BoneDamage)
            return;

        if (!TryComp<OrganComponent>(woundable, out var organ)
            || organ.Body is not { } body
            || !_consciousness.TryGetNerveSystem(body, out var nerveSys))
            return;

        TryRemovePainModifier(nerveSys.Value, woundable.Owner, "BoneDamage", nerveSys.Value.Comp);
    }

    #endregion
}

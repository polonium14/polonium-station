// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared.Body;
using Content.Shared.Mobs;
using Content.Shared.Rejuvenate;

namespace Content.Shared._Shitmed.Medical.Surgery.Consciousness.Systems;

public partial class ConsciousnessSystem
{
    private void InitProcess()
    {
        SubscribeLocalEvent<ConsciousnessComponent, MobStateChangedEvent>(OnMobStateChanged);
        // To prevent people immediately falling down as rejuvenated
        SubscribeLocalEvent<ConsciousnessComponent, RejuvenateEvent>(OnRejuvenate, after: [typeof(BodySystem)]);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganGotInsertedEvent>(OnOrganAdded);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganGotRemovedEvent>(OnOrganRemoved);

        SubscribeLocalEvent<ConsciousnessComponent, MapInitEvent>(OnConsciousnessMapInit);
    }

    private const string NerveSystemIdentifier = "nerveSystem";

    private void UpdatePassedOut(float frameTime)
    {
        var query = EntityQueryEnumerator<ConsciousnessComponent>();
        while (query.MoveNext(out var ent, out var consciousness))
        {
            if (consciousness.ForceDead
                || _timing.CurTime < consciousness.NextConsciousnessUpdate)
                continue;

            consciousness.NextConsciousnessUpdate = _timing.CurTime + consciousness.ConsciousnessUpdateTime;

            foreach (var modifier in consciousness.Modifiers.Where(modifier => modifier.Value.Time < _timing.CurTime).ToList())
                RemoveConsciousnessModifier(ent, modifier.Key.Item1, modifier.Key.Item2, consciousness);

            foreach (var multiplier in consciousness.Multipliers.Where(multiplier => multiplier.Value.Time < _timing.CurTime).ToList())
                RemoveConsciousnessMultiplier(ent, multiplier.Key.Item1, multiplier.Key.Item2, consciousness);

            if (consciousness.PassedOutTime < _timing.CurTime && consciousness.PassedOut)
            {
                consciousness.PassedOut = false;
                CheckConscious(ent, consciousness);
            }

            if (consciousness.ForceConsciousnessTime < _timing.CurTime && consciousness.ForceConscious)
            {
                consciousness.ForceConscious = false;
                CheckConscious(ent, consciousness);
            }
        }
    }

    private void OnMobStateChanged(EntityUid uid, ConsciousnessComponent component, MobStateChangedEvent args)
    {
        if (component.NerveSystem != default && !TerminatingOrDeleted(component.NerveSystem))
            _pain.HandleMobStateChanged(component.NerveSystem, args);

        if (args.NewMobState == MobState.Dead)
        {
            AddConsciousnessModifier(uid, uid, -component.Cap, "DeathThreshold", ConsciousnessModType.Pain, consciousness: component);
            // To prevent people from suddenly resurrecting while being dead. whoops

            foreach (var multiplier in
                     component.Multipliers.Where(multiplier => multiplier.Value.Type != ConsciousnessModType.Pain).ToList())
                RemoveConsciousnessMultiplier(uid, multiplier.Key.Item1, multiplier.Key.Item2, component);

            foreach (var modifier in
                     component.Modifiers.Where(modifier => modifier.Value.Type != ConsciousnessModType.Pain).ToList())
                RemoveConsciousnessModifier(uid, modifier.Key.Item1, modifier.Key.Item2, component);

            return;
        }

        // Leaving Dead (revived) - DeathThreshold was only ever meant to hold while actually
        // dead. Without this, it never gets removed by anything and permanently caps this
        // mob's consciousness at -Cap for the rest of its life, healed or not.
        if (args.OldMobState == MobState.Dead)
            RemoveConsciousnessModifier(uid, uid, "DeathThreshold", component);
    }

    private void OnRejuvenate(EntityUid uid, ConsciousnessComponent component, RejuvenateEvent args)
    {
        if (component.NerveSystem != default)
        {
            foreach (var painModifier in component.NerveSystem.Comp.Modifiers.ToList())
                _pain.TryRemovePainModifier(component.NerveSystem.Owner,
                    painModifier.Key.Item1,
                    painModifier.Key.Item2,
                    component.NerveSystem.Comp);

            foreach (var painMultiplier in component.NerveSystem.Comp.Multipliers.ToList())
                _pain.TryRemovePainMultiplier(component.NerveSystem.Owner,
                    painMultiplier.Key,
                    component.NerveSystem.Comp);

            foreach (var nerve in component.NerveSystem.Comp.Nerves)
                foreach (var painFeelsModifier in nerve.Value.PainFeelingModifiers.ToList())
                    _pain.TryRemovePainFeelsModifier(painFeelsModifier.Key.Item1, painFeelsModifier.Key.Item2, nerve.Key, nerve.Value);
        }

        foreach (var multiplier in
                 component.Multipliers.Where(multiplier => multiplier.Value.Type == ConsciousnessModType.Pain).ToList())
            RemoveConsciousnessMultiplier(uid, multiplier.Key.Item1, multiplier.Key.Item2, component);

        foreach (var modifier in
                 component.Modifiers.Where(modifier => modifier.Value.Type == ConsciousnessModType.Pain).ToList())
            RemoveConsciousnessModifier(uid, modifier.Key.Item1, modifier.Key.Item2, component);

        CheckRequiredParts(uid, component);
        ForceConscious(uid, TimeSpan.FromSeconds(1f), component);
    }

    private void OnConsciousnessMapInit(EntityUid uid, ConsciousnessComponent consciousness, MapInitEvent args)
    {
        if (consciousness.RawConsciousness < 0)
        {
            consciousness.RawConsciousness = consciousness.Cap;
            Dirty(uid, consciousness);
        }

        CheckConscious(uid, consciousness);
    }

    private void OnOrganAdded(Entity<ConsciousnessRequiredComponent> ent, ref OrganGotInsertedEvent args)
    {
        var body = args.Target;

       if (!_timing.IsFirstTimePredicted || !TryComp<ConsciousnessComponent>(body, out var consciousness))
            return;

        var component = ent.Comp;
        var uid = ent.Owner;

        if (consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value) && value.Item1 != null && value.Item1 != uid)
            Log.Warning($"ConsciousnessRequirementPart with duplicate Identifier {component.Identifier}:{uid} added to a body:" +
                             $" {body} this will result in unexpected behaviour! Old {component.Identifier} wielder: {value.Item1}");

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);

        if (component.Identifier == NerveSystemIdentifier)
            consciousness.NerveSystem = (uid, Comp<NerveSystemComponent>(uid));

        if (_timing.ApplyingState)
            return;

        CheckRequiredParts(body, consciousness);
    }

    private void OnOrganRemoved(Entity<ConsciousnessRequiredComponent> ent, ref OrganGotRemovedEvent args)
    {
        var oldBody = args.Target;

        if (!_timing.IsFirstTimePredicted || !TryComp<ConsciousnessComponent>(oldBody, out var consciousness))
            return;

        var component = ent.Comp;
        var uid = ent.Owner;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            Log.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier}:{uid} not found on body:{oldBody}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, value.Item2, true);

        if (component.Identifier == NerveSystemIdentifier)
            consciousness.NerveSystem = default;

        if (_timing.ApplyingState)
            return;

        CheckRequiredParts(oldBody, consciousness);
    }
}

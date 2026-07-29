// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chemistry.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.Audio;
using Content.Shared.Body.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Medical;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server.Abilities.Felinid;

public sealed partial class FelinidSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private HungerSystem _hungerSystem = default!;
    [Dependency] private VomitSystem _vomitSystem = default!;
    [Dependency] private SolutionContainerSystem _solutionSystem = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FelinidComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FelinidComponent, HairballActionEvent>(OnHairball);
        SubscribeLocalEvent<FelinidComponent, EatMouseActionEvent>(OnEatMouse);
        SubscribeLocalEvent<FelinidComponent, DidEquipHandEvent>(OnEquipped);
        SubscribeLocalEvent<FelinidComponent, DidUnequipHandEvent>(OnUnequipped);
        SubscribeLocalEvent<HairballComponent, ThrowDoHitEvent>(OnHairballHit);
        SubscribeLocalEvent<HairballComponent, GettingPickedUpAttemptEvent>(OnHairballPickupAttempt);
    }

    private readonly Queue<EntityUid> _remQueue = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var cat in _remQueue)
        {
            RemComp<CoughingUpHairballComponent>(cat);
        }
        _remQueue.Clear();

        var query = EntityQueryEnumerator<CoughingUpHairballComponent, FelinidComponent>();
        while (query.MoveNext(out var uid, out var hairballComp, out var catComp))
        {
            hairballComp.Accumulator += frameTime;
            if (hairballComp.Accumulator < hairballComp.CoughUpTime.TotalSeconds)
                continue;

            hairballComp.Accumulator = 0;
            SpawnHairball(uid, catComp);
            _remQueue.Enqueue(uid);
        }
    }

    private void OnInit(EntityUid uid, FelinidComponent component, ComponentInit args)
    {
        if (component.HairballAction != null)
            return;

        // _actionsSystem.AddAction(uid, ref component.HairballAction, component.HairballActionId); // funkystation - No
    }

    private void OnEquipped(EntityUid uid, FelinidComponent component, DidEquipHandEvent args)
    {
        if (!HasComp<FelinidFoodComponent>(args.Equipped))
            return;

        component.EatActionTarget = args.Equipped;

        _actionsSystem.AddAction(uid, ref component.EatAction, component.EatActionId);
    }

    private void OnUnequipped(EntityUid uid, FelinidComponent component, DidUnequipHandEvent args)
    {
        if (args.Unequipped != component.EatActionTarget)
            return;

        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (held == args.Unequipped || !HasComp<FelinidFoodComponent>(held))
                continue;

            component.EatActionTarget = held;
            return;
        }

        component.EatActionTarget = null;
        if (component.EatAction != null)
            _actionsSystem.RemoveAction(uid, component.EatAction.Value);
    }

    private void OnHairball(EntityUid uid, FelinidComponent component, HairballActionEvent args)
    {
        if (_inventorySystem.TryGetSlotEntity(uid, "mask", out var maskUid) &&
            TryComp<IngestionBlockerComponent>(maskUid, out var blocker) &&
            blocker.Enabled)
        {
            _popupSystem.PopupEntity(Loc.GetString("hairball-mask", ("mask", maskUid)), uid, uid);
            return;
        }

        _popupSystem.PopupEntity(Loc.GetString("hairball-cough", ("name", Identity.Entity(uid, EntityManager))), uid);
        _audio.PlayPvs("/Audio/Effects/Species/hairball.ogg", uid, AudioHelpers.WithVariation(0.15f));

        EnsureComp<CoughingUpHairballComponent>(uid);
        args.Handled = true;
    }

    private void OnEatMouse(EntityUid uid, FelinidComponent component, EatMouseActionEvent args)
    {
        if (component.EatActionTarget == null)
            return;

        if (!TryComp<HungerComponent>(uid, out var hunger))
            return;

        if (hunger.CurrentThreshold == HungerThreshold.Overfed)
        {
            _popupSystem.PopupEntity(Loc.GetString("food-system-you-cannot-eat-any-more"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (_inventorySystem.TryGetSlotEntity(uid, "mask", out var maskUid) &&
            TryComp<IngestionBlockerComponent>(maskUid, out var blocker) &&
            blocker.Enabled)
        {
            _popupSystem.PopupEntity(Loc.GetString("hairball-mask", ("mask", maskUid)), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (component.HairballAction != null)
        {
            // You get the charge back and that's it. Tough.
            _charges.SetCharges(component.HairballAction.Value, 1);
            _actionsSystem.SetEnabled(component.HairballAction.Value, true);
        }

        Del(component.EatActionTarget.Value);
        component.EatActionTarget = null;

        _audio.PlayPvs("/Audio/Items/eating_1.ogg", uid, AudioHelpers.WithVariation(0.15f));

        _hungerSystem.ModifyHunger(uid, 50f, hunger);

        if (component.EatAction != null)
            _actionsSystem.RemoveAction(uid, component.EatAction.Value);
    }

    private void SpawnHairball(EntityUid uid, FelinidComponent component)
    {
        var hairball = Spawn(component.HairballPrototype, Transform(uid).Coordinates);
        var hairballComp = Comp<HairballComponent>(hairball);

        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream) || bloodstream.MetabolitesSolution is not { } chemicals)
            return;

        var temp = _solutionSystem.SplitSolution(chemicals, 20);

        if (_solutionSystem.TryGetSolution(hairball, hairballComp.SolutionName, out var hairballSolution))
            _solutionSystem.TryAddSolution(hairballSolution.Value, temp);
    }

    private void OnHairballHit(EntityUid uid, HairballComponent component, ThrowDoHitEvent args)
    {
        if (HasComp<FelinidComponent>(args.Target) || !HasComp<StatusEffectsComponent>(args.Target))
            return;

        if (_robustRandom.Prob(0.2f))
            _vomitSystem.Vomit(args.Target);
    }

    private void OnHairballPickupAttempt(EntityUid uid, HairballComponent component, GettingPickedUpAttemptEvent args)
    {
        if (HasComp<FelinidComponent>(args.User) || !HasComp<StatusEffectsComponent>(args.User))
            return;

        if (_robustRandom.Prob(0.2f))
        {
            _vomitSystem.Vomit(args.User);
            args.Cancel();
        }
    }
}

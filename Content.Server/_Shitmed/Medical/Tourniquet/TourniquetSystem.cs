using System;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Tourniquet;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Shitmed.Medical.Tourniquet;

public sealed partial class TourniquetSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;

    private const string TourniquetContainerId = "Tourniquet";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TourniquetComponent, UseInHandEvent>(OnTourniquetUse);
        SubscribeLocalEvent<TourniquetComponent, AfterInteractEvent>(OnTourniquetAfterInteract);

        SubscribeLocalEvent<BodyComponent, TourniquetDoAfterEvent>(OnBodyDoAfter);
        SubscribeLocalEvent<BodyComponent, RemoveTourniquetDoAfterEvent>(OnTourniquetTakenOff);

        SubscribeLocalEvent<BodyComponent, GetVerbsEvent<InnateVerb>>(OnBodyGetVerbs);

        SubscribeLocalEvent<WoundComponent, WoundAddedEvent>(OnWoundAddedToTourniquetedOrgan);

        SubscribeLocalEvent<TourniquetedComponent, OrganGotRemovedEvent>(OnTourniquetedOrganRemoved);
    }

    private static string GetContainerId(Robust.Shared.Prototypes.ProtoId<OrganCategoryPrototype> category)
        => $"{TourniquetContainerId}-{category.Id}";

    private void OnWoundAddedToTourniquetedOrgan(Entity<WoundComponent> ent, ref WoundAddedEvent args)
    {
        if (!HasComp<TourniquetedComponent>(args.Component.HoldingWoundable))
            return;

        _bloodstream.TryAddBleedModifier(ent.Owner, "TourniquetPresent", 100, false, comp: null);
    }

    private bool TryTourniquet(EntityUid target, EntityUid user, EntityUid tourniquetEnt, TourniquetComponent tourniquet)
    {
        if (!TryComp<TargetingComponent>(user, out var targeting)
            || !HasComp<BodyComponent>(target)
            || !HasComp<ConsciousnessComponent>(target))
            return false;

        if (!LimbTargetMap.TryGetCategory(targeting.Target, out var category))
            return false;

        if (tourniquet.BlockedCategories.Contains(category))
        {
            _popup.PopupEntity(Loc.GetString("cant-put-tourniquet-here"), target, PopupType.MediumCaution);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("puts-on-a-tourniquet", ("user", user), ("part", GetPartName(category))), target, PopupType.Medium);
        _audio.PlayPvs(tourniquet.TourniquetPutOnSound, target, AudioParams.Default.WithVariation(0.125f).WithVolume(1f));

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager,
                user,
                tourniquet.Delay,
                new TourniquetDoAfterEvent(category),
                target,
                target: target,
                used: tourniquetEnt)
            {
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
        return true;
    }

    private void TakeOffTourniquet(EntityUid target, EntityUid user, EntityUid tourniquetEnt, TourniquetComponent tourniquet)
    {
        var partName = TryComp<OrganComponent>(tourniquet.OrganTourniqueted, out var organComp) && organComp.Category is { } cat
            ? GetPartName(cat)
            : Loc.GetString("target-zone-chest");

        _popup.PopupEntity(Loc.GetString("takes-off-a-tourniquet",
            ("user", user),
            ("part", partName)),
            target,
            PopupType.Medium);
        _audio.PlayPvs(tourniquet.TourniquetPutOffSound, target, AudioParams.Default.WithVariation(0.125f).WithVolume(1f));

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, tourniquet.RemoveDelay, new RemoveTourniquetDoAfterEvent(), target, target: target, used: tourniquetEnt)
            {
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnTourniquetUse(Entity<TourniquetComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryTourniquet(args.User, args.User, ent, ent))
            args.Handled = true;
    }

    private void OnTourniquetAfterInteract(Entity<TourniquetComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || args.Target == null)
            return;

        if (TryTourniquet(args.Target.Value, args.User, ent, ent))
            args.Handled = true;
    }

    private void OnBodyDoAfter(Entity<BodyComponent> ent, ref TourniquetDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<TourniquetComponent>(args.Used, out var tourniquet))
            return;

        var container = _container.EnsureContainer<ContainerSlot>(args.Target!.Value, GetContainerId(args.Category));
        if (container.ContainedEntity.HasValue)
        {
            _popup.PopupEntity(Loc.GetString("already-tourniqueted"), ent, PopupType.Medium);
            return;
        }

        if (ent.Comp.Organs is null
            || !LimbTargetMap.TryGetOrganByCategory(EntityManager, ent.Comp, args.Category, out var organ))
        {
            _popup.PopupEntity(Loc.GetString("missing-body-part"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!_container.Insert(args.Used.Value, container))
        {
            _popup.PopupEntity(Loc.GetString("cant-tourniquet"), ent, PopupType.Medium);
            return;
        }

        ApplyTourniquetEffects(args.Used.Value, organ);

        foreach (var childCategory in LimbTargetMap.GetCascadeChildren(args.Category))
        {
            if (LimbTargetMap.TryGetOrganByCategory(EntityManager, ent.Comp, childCategory, out var childOrgan))
                ApplyTourniquetEffects(args.Used.Value, childOrgan);
        }

        tourniquet.OrganTourniqueted = organ;
        args.Handled = true;
    }

    private void ApplyTourniquetEffects(EntityUid tourniquetEnt, EntityUid organ)
    {
        if (HasComp<NerveComponent>(organ))
            _pain.TryAddPainFeelsModifier(tourniquetEnt, "Tourniquet", organ, -10f);

        _bloodstream.TryAddBleedModifier(organ, "TourniquetPresent", 100, false, force: true);
        EnsureComp<TourniquetedComponent>(organ).TourniquetEntity = tourniquetEnt;
    }

    private void RemoveTourniquetEffects(EntityUid tourniquetEnt, EntityUid organ)
    {
        if (HasComp<NerveComponent>(organ))
            _pain.TryRemovePainFeelsModifier(tourniquetEnt, "Tourniquet", organ);

        _bloodstream.TryRemoveBleedModifier(organ, "TourniquetPresent", force: true);
        RemComp<TourniquetedComponent>(organ);
    }

    private void OnTourniquetedOrganRemoved(Entity<TourniquetedComponent> ent, ref OrganGotRemovedEvent args)
    {
        var organ = ent.Owner;
        var body = args.Target;
        var tourniquetItem = ent.Comp.TourniquetEntity;

        // Bulk teardown (map/body deletion, gibbing) fires this for every organ at once while
        // the tourniquet item and body are themselves mid-termination - touching a terminating
        // entity's container membership logs a hard error there's no real state left to fix, so
        // skip the cleanup entirely and let deletion take everything with it.
        if (TerminatingOrDeleted(tourniquetItem) || TerminatingOrDeleted(body))
            return;

        // Whatever tourniquet item put these effects on this organ didn't travel with it - strip
        // them here regardless of whether it's the tourniquet's primary organ or a cascade child,
        // otherwise a transplanted limb carries the bleed-block/pain-feels modifier to its new
        // body with no tourniquet item present to ever remove it.
        RemoveTourniquetEffects(tourniquetItem, organ);

        if (!TryComp<TourniquetComponent>(tourniquetItem, out var tourniquet) || tourniquet.OrganTourniqueted != organ)
            return;

        // This was the tourniquet's primary organ - fully take it off the rest of the body too:
        // strip any cascade children still attached, eject the item from its container (it falls
        // off rather than leaving the slot permanently occupied), and clear the back-reference.
        if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category is { } category)
        {
            foreach (var childCategory in LimbTargetMap.GetCascadeChildren(category))
            {
                if (TryComp<BodyComponent>(body, out var bodyComp)
                    && bodyComp.Organs is not null
                    && LimbTargetMap.TryGetOrganByCategory(EntityManager, bodyComp, childCategory, out var childOrgan))
                    RemoveTourniquetEffects(tourniquetItem, childOrgan);
            }

            if (_container.TryGetContainer(body, GetContainerId(category), out var container))
                _container.Remove(tourniquetItem, container);
        }

        tourniquet.OrganTourniqueted = null;
    }

    private void OnTourniquetTakenOff(Entity<BodyComponent> ent, ref RemoveTourniquetDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<TourniquetComponent>(args.Used, out var tourniquet))
            return;

        var organ = tourniquet.OrganTourniqueted;
        if (organ == null)
            return;

        if (!TryComp<OrganComponent>(organ.Value, out var primaryOrganComp) || primaryOrganComp.Category is not { } primaryCategory)
            return;

        if (!_container.TryGetContainer(ent, GetContainerId(primaryCategory), out var container))
            return;

        RemoveTourniquetEffects(args.Used.Value, organ.Value);

        foreach (var childCategory in LimbTargetMap.GetCascadeChildren(primaryCategory))
        {
            if (ent.Comp.Organs is not null && LimbTargetMap.TryGetOrganByCategory(EntityManager, ent.Comp, childCategory, out var childOrgan))
                RemoveTourniquetEffects(args.Used.Value, childOrgan);
        }

        _container.Remove(args.Used.Value, container);

        _hands.TryPickupAnyHand(args.User, args.Used.Value);
        tourniquet.OrganTourniqueted = null;

        args.Handled = true;
    }

    private void OnBodyGetVerbs(Entity<BodyComponent> ent, ref GetVerbsEvent<InnateVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var target = args.Target;
        var user = args.User;

        if (!TryComp<ContainerManagerComponent>(target, out var containerManager))
            return;

        foreach (var container in _container.GetAllContainers(target, containerManager))
        {
            var containerId = container.ID;
            if (!containerId.StartsWith(TourniquetContainerId, StringComparison.Ordinal))
                continue;

            foreach (var entity in container.ContainedEntities)
            {
                if (!TryComp<TourniquetComponent>(entity, out var tourniquet))
                    continue;

                var partName = TryComp<OrganComponent>(tourniquet.OrganTourniqueted, out var organComp) && organComp.Category is { } cat
                    ? GetPartName(cat)
                    : Loc.GetString("target-zone-chest");

                var capturedEntity = entity;
                var capturedTourniquet = tourniquet;
                InnateVerb verb = new()
                {
                    Act = () => TakeOffTourniquet(target, user, capturedEntity, capturedTourniquet),
                    Text = Loc.GetString("take-off-tourniquet", ("part", partName)),
                    Priority = 2,
                };
                args.Verbs.Add(verb);
            }
        }
    }

    private string GetPartName(Robust.Shared.Prototypes.ProtoId<OrganCategoryPrototype> category)
    {
        return LimbTargetMap.TryGetTarget(category, out var target)
            ? Loc.GetString($"target-zone-{target.ToString().ToLowerInvariant()}")
            : category.Id;
    }
}

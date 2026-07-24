// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Shitmed.Medical.Surgery;

public abstract partial class SharedSurgerySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] protected StatusEffectsSystem Status = default!;

    private EntityQuery<BodyComponent> _bodyQuery;
    private EntityQuery<StackComponent> _stackQuery;

    /// <summary>
    /// Cache of all surgery prototypes' singleton entities.
    /// Cleared after a prototype reload.
    /// </summary>
    private readonly Dictionary<EntProtoId, EntityUid> _surgeries = new();

    private readonly List<EntProtoId> _allSurgeries = new();

    /// <summary>
    /// Every surgery entity prototype id.
    /// Kept in sync with prototype reloads.
    /// </summary>
    public IReadOnlyList<EntProtoId> AllSurgeries => _allSurgeries;

    public override void Initialize()
    {
        base.Initialize();

        _bodyQuery = GetEntityQuery<BodyComponent>();
        _stackQuery = GetEntityQuery<StackComponent>();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<SurgeryTargetComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SurgeryTargetComponent, DoAfterAttemptEvent<SurgeryDoAfterEvent>>(OnBeforeTargetDoAfter);
        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryDoAfterEvent>(OnTargetDoAfter);
        SubscribeLocalEvent<SurgeryCloseIncisionConditionComponent, SurgeryValidEvent>(OnCloseIncisionValid);
        SubscribeLocalEvent<SurgeryHasBodyConditionComponent, SurgeryValidEvent>(OnHasBodyConditionValid);
        SubscribeLocalEvent<SurgeryPartConditionComponent, SurgeryValidEvent>(OnPartConditionValid);
        SubscribeLocalEvent<SurgeryOrganConditionComponent, SurgeryValidEvent>(OnOrganConditionValid);
        SubscribeLocalEvent<SurgeryWoundedConditionComponent, SurgeryValidEvent>(OnWoundedValid);
        SubscribeLocalEvent<SurgeryPartRemovedConditionComponent, SurgeryValidEvent>(OnPartRemovedConditionValid);
        SubscribeLocalEvent<SurgeryPartPresentConditionComponent, SurgeryValidEvent>(OnPartPresentConditionValid);
        SubscribeLocalEvent<SurgeryTraumaPresentConditionComponent, SurgeryValidEvent>(OnTraumaPresentConditionValid);
        SubscribeLocalEvent<SurgeryBleedsPresentConditionComponent, SurgeryValidEvent>(OnBleedsPresentConditionValid);
        SubscribeLocalEvent<SurgeryBodyComponentConditionComponent, SurgeryValidEvent>(OnBodyComponentConditionValid);
        SubscribeLocalEvent<SurgeryPartComponentConditionComponent, SurgeryValidEvent>(OnPartComponentConditionValid);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<SanitizedComponent, SurgerySanitizationEvent>(OnSanitization);
        SubscribeLocalEvent<SanitizedComponent, HeldRelayedEvent<SurgerySanitizationEvent>>(OnHeldSanitization);

        SubscribeLocalEvent<SurgeryMarkingConditionComponent, SurgeryValidEvent>(OnUnimplementedConditionValid);

        InitializeSteps();
        InitializeStart();

        LoadPrototypes();
    }

    private void OnHeldSanitization(Entity<SanitizedComponent> ent, ref HeldRelayedEvent<SurgerySanitizationEvent> args)
    {
        if (ent.Comp.WorksInHands)
            args.Args.Handled = true;
    }

    private void OnSanitization(Entity<SanitizedComponent> ent, ref SurgerySanitizationEvent args)
    {
        args.Handled = true;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _surgeries.Clear();
    }

    private void OnMapInit(Entity<SurgeryTargetComponent> ent, ref MapInitEvent args)
    {
        var data = new InterfaceData("SurgeryBui");
        _ui.SetUi(ent.Owner, SurgeryUIKey.Key, data);
    }

    private void OnBeforeTargetDoAfter(Entity<SurgeryTargetComponent> ent,
        ref DoAfterAttemptEvent<SurgeryDoAfterEvent> args)
    {
        if (_net.IsClient)
            return;

        if (args.Event.Target is not { } target)
        {
            args.Cancel();
            return;
        }

        if (!IsSurgeryValid(ent, target, args.Event.Surgery, args.Event.Step, args.Event.User, out var surgery, out var part, out var _))
        {
            Log.Warning($"Cancelling surgery doafter mid-way: {args.Event.Surgery}/{args.Event.Step} on {ToPrettyString(target)} of {ToPrettyString(ent)} - IsSurgeryValid failed.");
            args.Cancel();
            return;
        }

        if (IsStepComplete(ent, part, args.Event.Step, surgery))
        {
            if (!args.Event.Repeat)
                Log.Warning($"Cancelling surgery doafter mid-way: {args.Event.Surgery}/{args.Event.Step} on {ToPrettyString(target)} of {ToPrettyString(ent)} - step already complete.");
            args.Cancel();
        }
    }

    private void OnTargetDoAfter(Entity<SurgeryTargetComponent> ent, ref SurgeryDoAfterEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (args.Cancelled)
        {
            var alreadyComplete = args.Target is { } cancelledPart
                && IsSurgeryValid(ent, cancelledPart, args.Surgery, args.Step, args.User, out var cancelledSurgery, out _, out _)
                && IsStepComplete(ent, cancelledPart, args.Step, cancelledSurgery);

            if (!alreadyComplete)
            {
                Log.Warning($"Surgery step {args.Step} of {args.Surgery} on {ToPrettyString(ent)} was cancelled for {ToPrettyString(args.User)}.");
                _popup.PopupClient(Loc.GetString("surgery-error-step-interrupted"), args.User, args.User, PopupType.SmallCaution);
            }

            RaiseStepFailed(args.User, ent, args.Surgery, args.Step);
            return;
        }

        var tool = _hands.GetActiveItemOrSelf(args.User);

        if (args.Handled || args.Target is not { } target)
            return;

        var valid = IsSurgeryValid(ent, target, args.Surgery, args.Step, args.User, out var surgery, out var part, out var step);

        // PreviousStepsComplete logs its own, more specific warning (which step blocks and why),
        // so only the validity failure needs one here.
        if (!valid)
            Log.Warning($"Surgery step {args.Step} of {args.Surgery} on {ToPrettyString(ent)} (part {ToPrettyString(target)}) became invalid before {ToPrettyString(args.User)} finished it.");

        if (!valid || !PreviousStepsComplete(ent, part, surgery, args.Step, args.User))
        {
            _popup.PopupClient(Loc.GetString("surgery-error-step-interrupted"), args.User, args.User, PopupType.SmallCaution);
            RaiseStepFailed(args.User, ent, args.Surgery, args.Step);
            return;
        }

        if (!CanPerformStep(args.User, ent, part, step, tool, true))
        {
            Log.Warning($"{ToPrettyString(args.User)} tried to complete a surgery step without the right tool in hand.");
            return;
        }

        var complete = IsStepComplete(ent, part, args.Step, surgery);

        args.Repeat = HasComp<SurgeryRepeatableStepComponent>(step) && !complete;
        var ev = new SurgeryStepEvent(args.User, ent, part, tool, surgery, step);
        RaiseLocalEvent(step, ref ev);
        RaiseLocalEvent(args.User, ref ev);

        // consume the tool if it's something like using LV cable as stitches
        if (args.ToolUsed)
        {
            if (_stackQuery.HasComp(tool))
                _stack.ReduceCount(tool, 1);
            else
                PredictedQueueDel(tool);
        }

        RefreshUI(ent);
    }

    private void RaiseStepFailed(EntityUid user, EntityUid body, EntProtoId surgery, EntProtoId step)
    {
        var failEv = new SurgeryStepFailedEvent(user, body, surgery, step);
        RaiseLocalEvent(user, ref failEv);
    }

    private void OnCloseIncisionValid(Entity<SurgeryCloseIncisionConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!HasComp<IncisionOpenComponent>(args.Part) ||
            !HasComp<BleedersClampedComponent>(args.Part) ||
            !HasComp<SkinRetractedComponent>(args.Part) ||
            !HasComp<BodyPartReattachedComponent>(args.Part) ||
            !HasComp<InternalBleedersClampedComponent>(args.Part))
        {
            args.Cancelled = true;
        }
    }

    private void OnWoundedValid(Entity<SurgeryWoundedConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var hasWoundable = TryComp(args.Part, out WoundableComponent? partWoundable);

        if (!hasWoundable)
        {
            args.Cancelled = true;
            return;
        }

        var point = _wounds.GetWoundableSeverityPoint(args.Part, partWoundable, ent.Comp.DamageGroup, healable: true);

        // Wound-less raw damage (below TryCreateWound's minorThreshold, see GetGroupDamage's
        // doc comment) still needs a surgery to clear it, or it sits stuck forever.
        var rawDamage = point > 0 ? FixedPoint2.Zero : _wounds.GetGroupDamage(args.Part, ent.Comp.DamageGroup);

        if (point <= 0 && rawDamage <= 0)
            args.Cancelled = true;
    }

    private void OnBodyComponentConditionValid(Entity<SurgeryBodyComponentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var present = true;
        foreach (var reg in ent.Comp.Components.Values)
        {
            var compType = reg.Component.GetType();
            if (!HasComp(args.Body, compType))
                present = false;
        }

        if (ent.Comp.Inverse ? present : !present)
            args.Cancelled = true;
    }

    private void OnPartComponentConditionValid(Entity<SurgeryPartComponentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var present = true;
        foreach (var reg in ent.Comp.Components.Values)
        {
            var compType = reg.Component.GetType();
            if (!HasComp(args.Part, compType))
                present = false;
        }

        args.Cancelled |= present == ent.Comp.Inverse;
    }

    private void OnUnimplementedConditionValid<TComp>(Entity<TComp> ent, ref SurgeryValidEvent args) where TComp : IComponent
    {
        Log.Error($"Surgery {ent} references {typeof(TComp).Name}, which is ported but not implemented in this fork. Refusing to validate.");
        args.Cancelled = true;
    }

    private void OnHasBodyConditionValid(Entity<SurgeryHasBodyConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (CompOrNull<OrganComponent>(args.Part)?.Body == null)
            args.Cancelled = true;
    }

    private void OnPartConditionValid(Entity<SurgeryPartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Category is not { } category)
        {
            args.Cancelled = true;
            return;
        }

        var valid = ent.Comp.Categories.Contains(category);

        if (ent.Comp.Inverse ? valid : !valid)
            args.Cancelled = true;
    }

    private void OnOrganConditionValid(Entity<SurgeryOrganConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<BodyComponent>(args.Body, out var body))
        {
            args.Cancelled = true;
            return;
        }

        var present = LimbTargetMap.TryGetOrganByCategory(EntityManager, body, ent.Comp.Category, out var organUid);

        if (!ent.Comp.Inverse)
        {
            if (!present)
                args.Cancelled = true;
            return;
        }

        // Inverse: valid when the organ is absent, or (if Reattaching) present but still
        // tagged as freshly reattached — keeps an "organ missing" condition valid for one more
        // step right after a transplant, until the affix step clears the tag.
        if (present && (!ent.Comp.Reattaching || !HasComp<OrganReattachedComponent>(organUid)))
            args.Cancelled = true;
    }

    private void OnPartRemovedConditionValid(Entity<SurgeryPartRemovedConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<BodyComponent>(args.Body, out var body))
        {
            args.Cancelled = true;
            return;
        }

        if (LimbTargetMap.TryGetOrganByCategory(EntityManager, body, ent.Comp.Category, out var limb)
            && !HasComp<BodyPartReattachedComponent>(limb))
            args.Cancelled = true;
    }

    private void OnPartPresentConditionValid(Entity<SurgeryPartPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Part == EntityUid.Invalid
            || !HasComp<OrganComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnTraumaPresentConditionValid(Entity<SurgeryTraumaPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Cancelled)
            return;

        // not inverted = cancel if no trauma present
        // inverted = cancel if trauma present
        if (_trauma.HasWoundableTrauma(args.Part, ent.Comp.TraumaType) == ent.Comp.Inverted)
            args.Cancelled = true;
    }

    private void OnBleedsPresentConditionValid(Entity<SurgeryBleedsPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<WoundableComponent>(args.Part, out var woundable))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.Inverted == woundable.Bleeds > 0
            && !HasComp<BleedersClampedComponent>(args.Part))
            args.Cancelled = true;
    }

    protected bool IsSurgeryValid(EntityUid body, EntityUid targetPart, EntProtoId surgery, EntProtoId stepId,
        EntityUid user, out Entity<SurgeryComponent> surgeryEnt, out EntityUid part, out EntityUid step)
    {
        surgeryEnt = default;
        part = default;
        step = default;

        if (!HasComp<SurgeryTargetComponent>(body) ||
            !IsLyingDown(body, user) ||
            GetSingleton(surgery) is not { } surgeryEntId ||
            !TryComp(surgeryEntId, out SurgeryComponent? surgeryComp) ||
            !surgeryComp.Steps.Contains(stepId) ||
            GetSingleton(stepId) is not { } stepEnt
            || !HasComp<OrganComponent>(targetPart)
            && !_bodyQuery.HasComp(targetPart))
            return false;

        TryComp<OrganComponent>(targetPart, out var targetOrgan);
        var ev = new SurgeryValidEvent(body, targetPart, Category: targetOrgan?.Category);
        if (_timing.IsFirstTimePredicted)
        {
            RaiseLocalEvent(stepEnt, ref ev);
            if (!ev.Cancelled)
                RaiseLocalEvent(surgeryEntId, ref ev);
        }

        if (ev.Cancelled)
            return false;

        surgeryEnt = (surgeryEntId, surgeryComp);
        part = targetPart;
        step = stepEnt;
        return true;
    }

    public EntityUid? GetSingleton(EntProtoId surgeryOrStep)
    {
        if (!_prototypes.HasIndex(surgeryOrStep))
            return null;

        // This (for now) assumes that surgery entity data remains unchanged between client
        // and server
        // if it does not you get the bullet
        if (!_surgeries.TryGetValue(surgeryOrStep, out var ent) || TerminatingOrDeleted(ent))
        {
            ent = Spawn(surgeryOrStep, MapCoordinates.Nullspace);
            _surgeries[surgeryOrStep] = ent;
        }

        return ent;
    }

    /// <summary>
    /// Checks if someone is lying down (and is able to)
    /// Shows a popup if this is run on the user's client.
    /// </summary>
    public bool IsLyingDown(EntityUid entity, EntityUid user)
    {
        if (_standing.IsDown(entity))
            return true;

        // you can't otherwise operate on something with no buckle
        // just let people do surgery on goliaths and shit
        if (!TryComp<BuckleComponent>(entity, out var buckle))
            return true;

        if (TryComp<StrapComponent>(buckle.BuckledTo, out var strap))
        {
            var rotation = strap.Rotation;
            if (rotation.GetCardinalDir() is Direction.West or Direction.East)
                return true;
        }

        _popup.PopupClient(Loc.GetString("surgery-error-laying"), user, user);
        return false;
    }

    protected virtual void RefreshUI(EntityUid body)
    {
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>())
            return;

        LoadPrototypes();
    }

    private void LoadPrototypes()
    {
        // Cache is probably invalid so delete it
        foreach (var uid in _surgeries.Values)
        {
            Del(uid);
        }
        _surgeries.Clear();

        _allSurgeries.Clear();
        foreach (var entity in _prototypes.EnumeratePrototypes<EntityPrototype>())
            if (entity.HasComponent<SurgeryComponent>())
                _allSurgeries.Add(new EntProtoId(entity.ID));
    }
}

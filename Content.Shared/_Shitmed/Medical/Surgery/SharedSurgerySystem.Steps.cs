using System.Linq;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Effects.Step;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Shitmed.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private static readonly ProtoId<DamageTypePrototype> SepsisDamageType = "Poison";

    private EntityQuery<OrganComponent> _organQuery;
    private EntityQuery<SurgeryIgnoreClothingComponent> _ignoreQuery;
    private EntityQuery<SurgeryStepComponent> _stepQuery;
    private EntityQuery<SurgeryToolComponent> _toolQuery;

    private readonly List<EntityUid> _nextStepList = new();

    private void InitializeSteps()
    {
        _organQuery = GetEntityQuery<OrganComponent>();
        _ignoreQuery = GetEntityQuery<SurgeryIgnoreClothingComponent>();
        _stepQuery = GetEntityQuery<SurgeryStepComponent>();
        _toolQuery = GetEntityQuery<SurgeryToolComponent>();

        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepEvent>(OnToolStep);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepCompleteCheckEvent>(OnToolCheck);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryCanPerformStepEvent>(OnToolCanPerform);
        SubscribeLocalEvent<SurgeryOperatingTableConditionComponent, SurgeryCanPerformStepEvent>(OnTableCanPerform);

        SubSurgery<SurgeryTendWoundsEffectComponent>(OnTendWoundsStep, OnTendWoundsCheck);
        SubSurgery<SurgeryAddPartStepComponent>(OnAddPartStep, OnAddPartCheck);
        SubSurgery<SurgeryAffixPartStepComponent>(OnAffixPartStep, OnAffixPartCheck);
        SubSurgery<SurgeryRemovePartStepComponent>(OnRemovePartStep, OnRemovePartCheck);
        SubSurgery<SurgeryAddOrganStepComponent>(OnAddOrganStep, OnAddOrganCheck);
        SubSurgery<SurgeryRemoveOrganStepComponent>(OnRemoveOrganStep, OnRemoveOrganCheck);
        SubSurgery<SurgeryAffixOrganStepComponent>(OnAffixOrganStep, OnAffixOrganCheck);
        SubSurgery<SurgeryTraumaTreatmentStepComponent>(OnTraumaTreatmentStep, OnTraumaTreatmentCheck);
        SubSurgery<SurgeryBleedsTreatmentStepComponent>(OnBleedsTreatmentStep, OnBleedsTreatmentCheck);
        SubSurgery<SurgeryStepPainInflicterComponent>(OnPainInflicterStep, OnPainInflicterCheck);
        Subs.BuiEvents<SurgeryTargetComponent>(SurgeryUIKey.Key, subs =>
        {
            subs.Event<SurgeryStepChosenBuiMsg>(OnSurgeryTargetStepChosen);
        });

        SubscribeLocalEvent<SurgeryStepCavityEffectComponent, SurgeryStepCompleteCheckEvent>(OnUnimplementedStepCheck);
        SubscribeLocalEvent<SurgeryAddMarkingStepComponent, SurgeryStepCompleteCheckEvent>(OnUnimplementedStepCheck);
        SubscribeLocalEvent<SurgeryRemoveMarkingStepComponent, SurgeryStepCompleteCheckEvent>(OnUnimplementedStepCheck);
    }

    private void OnUnimplementedStepCheck<TComp>(Entity<TComp> ent, ref SurgeryStepCompleteCheckEvent args) where TComp : IComponent
    {
        Log.Error($"Surgery step {ent} references {typeof(TComp).Name}, which is ported but not implemented in this fork. Refusing to complete the step.");
        args.Cancelled = true;
    }

    private void SubSurgery<TComp>(EntityEventRefHandler<TComp, SurgeryStepEvent> onStep,
        EntityEventRefHandler<TComp, SurgeryStepCompleteCheckEvent> onComplete) where TComp : IComponent
    {
        SubscribeLocalEvent(onStep);
        SubscribeLocalEvent(onComplete);
    }

    #region Event Methods

    private void OnToolStep(Entity<SurgeryStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryToolAudio(ent, args))
            return;

        ApplyComponentChanges(args, ent.Comp);
        HandleSanitization(args);
    }

    private void ApplyComponentChanges(SurgeryStepEvent args, SurgeryStepComponent comp)
    {
        AddOrRemoveComponentsToEntity(args.Part, comp.Add);
        AddOrRemoveComponentsToEntity(args.Part, comp.Remove, true);
        AddOrRemoveComponentsToEntity(args.Body, comp.BodyAdd);
        AddOrRemoveComponentsToEntity(args.Body, comp.BodyRemove, true);
    }

    private void OnToolCheck(Entity<SurgeryStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (CheckComponentChanges(ent.Comp, args))
            args.Cancelled = true;
    }

    private bool CheckComponentChanges(SurgeryStepComponent comp, SurgeryStepCompleteCheckEvent args)
    {
        return TryToolCheck(comp.Add, args.Part) ||
               TryToolCheck(comp.Remove, args.Part, checkMissing: false) ||
               TryToolCheck(comp.BodyAdd, args.Body) ||
               TryToolCheck(comp.BodyRemove, args.Body, checkMissing: false);
    }

    private void OnToolCanPerform(Entity<SurgeryStepComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (args.IsInvalid)
            return;

        // NONE means this part category has no mapped clothing slot to check (CanPerformStep's
        // switch) - InventorySlotEnumerator's own ctor asserts flags != NONE, so skip the
        // check entirely rather than crashing.
        if (args.TargetSlots != SlotFlags.NONE
            && !_ignoreQuery.HasComp(args.User)
            && !_ignoreQuery.HasComp(args.Tool)
            && _inventory.TryGetContainerSlotEnumerator(args.Body, out var containerSlotEnumerator, args.TargetSlots))
        {
            while (containerSlotEnumerator.MoveNext(out var containerSlot))
            {
                if (!containerSlot.ContainedEntity.HasValue)
                    continue;

                args.Invalid = StepInvalidReason.Armor;
                args.Popup = Loc.GetString("surgery-ui-window-steps-error-armor");
                return;
            }
        }

        if (ent.Comp.Tool == null)
            return;

        foreach (var reg in ent.Comp.Tool.Values)
        {
            if (GetSurgeryComp(args.Tool, reg.Component) is { } data)
            {
                args.ValidTool = data;
                return; // multiple required tools isn't supported so just return
            }

            args.Invalid = StepInvalidReason.MissingTool;

            if (reg.Component is ISurgeryToolComponent required)
                args.Popup = $"You need {required.ToolName} to perform this step!";
            else
                Log.Error($"Surgery step {ToPrettyString(ent)} wants bad component {reg.Component} which isn't a ISurgeryTool");

            return;
        }
    }

    private void OnTableCanPerform(Entity<SurgeryOperatingTableConditionComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (args.IsInvalid)
            return;

        // mobs that can't be buckled can never be operated because of this check
        if (!TryComp(args.Body, out BuckleComponent? buckle) ||
            !HasComp<OperatingTableComponent>(buckle.BuckledTo))
        {
            args.Invalid = StepInvalidReason.NeedsOperatingTable;
        }
    }

    private void OnTendWoundsStep(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepEvent args)
    {
        var healableSeverity = _wounds.GetWoundableSeverityPoint(args.Part, damageGroup: ent.Comp.MainGroup, healable: true);
        var rawDamage = _wounds.GetGroupDamage(args.Part, ent.Comp.MainGroup);

        if (healableSeverity <= 0 && rawDamage <= 0)
            return;

        // Right now the bonus is based off the body's total damage, maybe we could make it based off each part in the future.
        var severity = _wounds.GetWoundableSeverityPoint(args.Part, damageGroup: ent.Comp.MainGroup);

        // No wound to derive severity from (damage too small to have ever cleared
        // TryCreateWound's minorThreshold) - heal straight off the raw damage instead, or this
        // step would be a no-op despite OnWoundedValid making it available for this case.
        var bonus = ent.Comp.HealMultiplier * (severity > 0 ? severity : rawDamage);

        if (_mobState.IsDead(args.Body))
            bonus *= 0.2;

        // The bonus is a percentage of what's left, so it decays toward zero the longer
        // healing goes on - without a floor, repeated clicks approach but never actually
        // reach full heal. Guarantee at least 1 point of progress per click.
        if (bonus < 1)
            bonus = 1;

        var adjustedDamage = new DamageSpecifier(ent.Comp.Damage);

        var group = _prototypes.Index<DamageGroupPrototype>(ent.Comp.MainGroup);
        foreach (var type in group.DamageTypes)
            adjustedDamage.DamageDict[type] -= bonus;

        var ev = new SurgeryStepDamageEvent(args.User, args.Body, args.Part, args.Surgery, adjustedDamage, 0.5f);
        RaiseLocalEvent(args.Body, ref ev);
    }

    private void OnTendWoundsCheck(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (_wounds.HasDamageOfGroup(args.Part, ent.Comp.MainGroup) || _wounds.GetGroupDamage(args.Part, ent.Comp.MainGroup) > 0)
            args.Cancelled = true;
    }

    private void OnAddPartStep(Entity<SurgeryAddPartStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp(args.Surgery, out SurgeryPartRemovedConditionComponent? removedComp)
            || !_organQuery.TryComp(args.Tool, out var toolOrgan)
            || toolOrgan.Category != removedComp.Category
            || !TryComp<BodyComponent>(args.Body, out var body)
            || body.Organs is null
            || !_container.Insert(args.Tool, body.Organs))
            return;

        if (HasComp<WoundableComponent>(args.Tool))
            _wounds.RecomputeWoundableSeverity(args.Tool);

        EnsureComp<BodyPartReattachedComponent>(args.Tool);

        if (toolOrgan.Category?.Id is "LegLeft" or "LegRight" or "FootLeft" or "FootRight" or "ArmLeft" or "ArmRight")
            _trauma.RefreshLimbMovementSpeed(args.Body);
    }

    private void OnAffixPartStep(Entity<SurgeryAffixPartStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (HasComp<WoundableComponent>(args.Part))
            _wounds.TryHealWoundsOnWoundable(args.Part, FixedPoint2.New(12), out _);

        RemComp<BodyPartReattachedComponent>(args.Part);
    }

    private void OnAffixPartCheck(Entity<SurgeryAffixPartStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (HasComp<BodyPartReattachedComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnAddPartCheck(Entity<SurgeryAddPartStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp(args.Surgery, out SurgeryPartRemovedConditionComponent? removedComp)
            || !TryComp<BodyComponent>(args.Body, out var body))
            return;

        if (!LimbTargetMap.TryGetOrganByCategory(EntityManager, body, removedComp.Category, out _))
            args.Cancelled = true;
    }

    private void OnRemovePartStep(Entity<SurgeryRemovePartStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_organQuery.TryComp(args.Part, out var organ) || organ.Body != args.Body)
            return;

        if (HasComp<WoundableComponent>(args.Part))
        {
            _wounds.AmputateWoundableSafely(args.Part);
        }
        else if (TryComp<BodyComponent>(args.Body, out var body) && body.Organs is not null)
        {
            _container.Remove(args.Part, body.Organs, force: true);

            if (organ.Category?.Id is "FootLeft" or "FootRight")
                _trauma.RefreshLimbMovementSpeed(args.Body);
        }

        _hands.TryPickupAnyHand(args.User, args.Part);
    }

    private void OnRemovePartCheck(Entity<SurgeryRemovePartStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!_organQuery.TryComp(args.Part, out var organ) || organ.Body == args.Body)
            args.Cancelled = true;
    }

    private void OnAddOrganStep(Entity<SurgeryAddOrganStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_organQuery.TryComp(args.Part, out var partOrgan)
            || partOrgan.Body != args.Body
            || !TryComp(args.Surgery, out SurgeryOrganConditionComponent? organComp)
            || !_organQuery.TryComp(args.Tool, out var insertedOrgan)
            || insertedOrgan.Category != organComp.Category
            || !TryComp<BodyComponent>(args.Body, out var body)
            || body.Organs is null
            || !_container.Insert(args.Tool, body.Organs))
            return;

        EnsureComp<OrganReattachedComponent>(args.Tool);

        var ev = new SurgeryStepDamageChangeEvent(args.User, args.Body, args.Part, ent);
        RaiseLocalEvent(ent, ref ev);
        args.Complete = true;
    }

    private void OnAddOrganCheck(Entity<SurgeryAddOrganStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp<SurgeryOrganConditionComponent>(args.Surgery, out var organComp)
            || !_organQuery.TryComp(args.Part, out var partOrgan)
            || partOrgan.Body != args.Body
            || !TryComp<BodyComponent>(args.Body, out var body))
            return;

        if (!LimbTargetMap.TryGetOrganByCategory(EntityManager, body, organComp.Category, out _))
            args.Cancelled = true;
    }

    private void OnAffixOrganStep(Entity<SurgeryAffixOrganStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp(args.Surgery, out SurgeryOrganConditionComponent? organComp) || !organComp.Reattaching)
            return;

        if (!TryComp<BodyComponent>(args.Body, out var body)
            || !LimbTargetMap.TryGetOrganByCategory(EntityManager, body, organComp.Category, out var organUid))
            return;

        RemComp<OrganReattachedComponent>(organUid);
    }

    private void OnAffixOrganCheck(Entity<SurgeryAffixOrganStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp(args.Surgery, out SurgeryOrganConditionComponent? organComp) || !organComp.Reattaching)
            return;

        if (!TryComp<BodyComponent>(args.Body, out var body)
            || !LimbTargetMap.TryGetOrganByCategory(EntityManager, body, organComp.Category, out var organUid))
            return;

        if (HasComp<OrganReattachedComponent>(organUid))
            args.Cancelled = true;
    }

    private void OnRemoveOrganStep(Entity<SurgeryRemoveOrganStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp<SurgeryOrganConditionComponent>(args.Surgery, out var organComp)
            || !TryComp<BodyComponent>(args.Body, out var body)
            || body.Organs is null
            || !LimbTargetMap.TryGetOrganByCategory(EntityManager, body, organComp.Category, out var organUid))
            return;

        _container.Remove(organUid, body.Organs, force: true);
        _hands.TryPickupAnyHand(args.User, organUid);
    }

    private void OnRemoveOrganCheck(Entity<SurgeryRemoveOrganStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp<SurgeryOrganConditionComponent>(args.Surgery, out var organComp)
            || !_organQuery.TryComp(args.Part, out var partOrgan)
            || partOrgan.Body != args.Body
            || !TryComp<BodyComponent>(args.Body, out var body))
            return;

        if (LimbTargetMap.TryGetOrganByCategory(EntityManager, body, organComp.Category, out _))
            args.Cancelled = true;
    }

    private void OnTraumaTreatmentStep(Entity<SurgeryTraumaTreatmentStepComponent> ent, ref SurgeryStepEvent args)
    {
        var healAmount = ent.Comp.Amount;
        switch (ent.Comp.TraumaType)
        {
            case TraumaType.OrganDamage:
                if (!TryComp<BodyComponent>(args.Body, out var body) || body.Organs is null)
                    break;

                foreach (var organUid in body.Organs.ContainedEntities.ToList())
                {
                    if (!TryComp<OrganIntegrityComponent>(organUid, out var organIntegrity))
                        continue;

                    foreach (var modifier in organIntegrity.IntegrityModifiers.ToList())
                    {
                        var delta = healAmount - modifier.Value;
                        if (delta > 0)
                        {
                            healAmount -= modifier.Value;
                            _trauma.TryRemoveOrganDamageModifier(
                                organUid,
                                modifier.Key.Item2,
                                modifier.Key.Item1,
                                organIntegrity);
                        }
                        else
                        {
                            _trauma.TryChangeOrganDamageModifier(
                                organUid,
                                -healAmount,
                                modifier.Key.Item2,
                                modifier.Key.Item1,
                                organIntegrity);
                            break;
                        }
                    }
                }

                break;

            case TraumaType.BoneDamage:
                if (!TryComp<WoundableComponent>(args.Part, out var woundable) || woundable.Bone is null)
                    return;

                var bone = woundable.Bone.ContainedEntities.FirstOrNull();
                if (bone == null || !TryComp<BoneComponent>(bone, out var boneComp))
                    return;

                _trauma.ApplyDamageToBone(bone.Value, -healAmount, boneComp);
                break;

            case TraumaType.Dismemberment:
                if (_trauma.TryGetWoundableTrauma(args.Part, out var traumas, TraumaType.Dismemberment))
                    foreach (var trauma in traumas)
                        _trauma.RemoveTrauma(trauma);

                break;
        }
    }

    private void OnTraumaTreatmentCheck(Entity<SurgeryTraumaTreatmentStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (_trauma.HasWoundableTrauma(args.Part, ent.Comp.TraumaType))
            args.Cancelled = true;
    }

    private void OnBleedsTreatmentStep(Entity<SurgeryBleedsTreatmentStepComponent> ent, ref SurgeryStepEvent args)
    {
        var healAmount = ent.Comp.Amount;
        foreach (var woundEnt in _wounds.GetWoundableWounds(args.Part))
        {
            if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleeds))
                continue;

            if (healAmount - bleeds.Scaling > 0)
            {
                healAmount -= bleeds.Scaling;

                bleeds.BleedingAmountRaw = 0;
                bleeds.Scaling = 0;

                bleeds.IsBleeding = false; // Won't bleed as long as it's not reopened

                Dirty(woundEnt, bleeds);
            }
            else
            {
                bleeds.Scaling -= healAmount;
                Dirty(woundEnt, bleeds);
                break;
            }
        }
    }

    private void OnBleedsTreatmentCheck(Entity<SurgeryBleedsTreatmentStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        foreach (var woundEnt in _wounds.GetWoundableWounds(args.Part))
        {
            if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleedsInflicter)
                || !bleedsInflicter.IsBleeding)
                continue;

            args.Cancelled = true;
            break;
        }
    }

    private void OnPainInflicterStep(Entity<SurgeryStepPainInflicterComponent> ent, ref SurgeryStepEvent args)
    {
        var ev = new SurgeryPainEvent();
        RaiseLocalEvent(args.Body, ev);
        if (ev.Cancelled)
            return;

        if (!_consciousness.TryGetNerveSystem(args.Body, out var nerveSys))
            return;

        var painToInflict = ent.Comp.Amount;
        if (Status.HasEffectComp<ForcedSleepingStatusEffectComponent>(args.Body))
            painToInflict *= ent.Comp.SleepModifier;

        if (!_pain.TryChangePainModifier(
                nerveSys.Value.Owner,
                args.Part,
                "SurgeryPain",
                painToInflict,
                nerveSys,
                ent.Comp.PainDuration,
                ent.Comp.PainType))
        {
            _pain.TryAddPainModifier(nerveSys.Value.Owner,
                args.Part,
                "SurgeryPain",
                painToInflict,
                ent.Comp.PainType,
                nerveSys,
                ent.Comp.PainDuration);
        }
    }

    private void OnPainInflicterCheck(Entity<SurgeryStepPainInflicterComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!_consciousness.TryGetNerveSystem(args.Body, out var nerveSys))
            return;

        if (!_pain.TryGetPainModifier(nerveSys.Value.Owner, args.Part, "SurgeryPain_wound", out _, nerveSys)
            && !_pain.TryGetPainModifier(nerveSys.Value.Owner, args.Part, "SurgeryPain_trauma", out _, nerveSys))
            args.Cancelled = true;
    }

    private void OnSurgeryTargetStepChosen(Entity<SurgeryTargetComponent> ent, ref SurgeryStepChosenBuiMsg args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var user = args.Actor;
        if (GetEntity(args.Entity) is { } body &&
            GetEntity(args.Part) is { } targetPart)
        {
            if (!TryDoSurgeryStep(body, targetPart, user, args.Surgery, args.Step, out var error))
                PopupStepInvalidReason(error, user);
        }
    }

    private void PopupStepInvalidReason(StepInvalidReason error, EntityUid user)
    {
        var locKey = error switch
        {
            StepInvalidReason.MissingSkills => "surgery-ui-window-steps-error-skills",
            StepInvalidReason.NeedsOperatingTable => "surgery-ui-window-steps-error-table",
            StepInvalidReason.MissingPreviousSteps => "surgery-ui-window-steps-error-previous",
            StepInvalidReason.StepCompleted => "surgery-ui-window-steps-error-completed",
            _ => null,
        };

        if (locKey != null)
            _popup.PopupClient(Loc.GetString(locKey), user, user, PopupType.SmallCaution);
    }

    #endregion

    #region Helper Methods

    private void HandleSanitization(SurgeryStepEvent args)
    {
        if (_inventory.TryGetSlotEntity(args.User, "gloves", out var _)
            && _inventory.TryGetSlotEntity(args.User, "mask", out var _))
            return;

        var sepsisEv = new SurgerySanitizationEvent();
        RaiseLocalEvent(args.User, sepsisEv);
        if (sepsisEv.Handled)
            return;

        if (TryComp<SurgeryTargetComponent>(args.Body, out var surgeryTargetComponent) &&
            surgeryTargetComponent.SepsisImmune)
            return;

        var sepsis = new DamageSpecifier(_prototypes.Index(SepsisDamageType), 5);
        var ev = new SurgeryStepDamageEvent(args.User, args.Body, args.Part, args.Surgery, sepsis, 0.5f);
        RaiseLocalEvent(args.Body, ref ev);
    }

    private bool TryToolAudio(Entity<SurgeryStepComponent> ent, SurgeryStepEvent args)
    {
        if (ent.Comp.Tool == null)
            return true;

        foreach (var reg in ent.Comp.Tool.Values)
        {
            if (!HasSurgeryComp(args.Tool, reg.Component))
                return false;

            if (_toolQuery.CompOrNull(args.Tool)?.EndSound is not { } sound)
                continue;
            _audio.PlayPredicted(sound, args.Tool, args.User);
            break; // no overlaying sounds
        }

        return true;
    }

    private void AddOrRemoveComponentsToEntity(EntityUid ent, ComponentRegistry? componentRegistry, bool remove = false)
    {
        if (componentRegistry == null)
            return;
        foreach (var reg in componentRegistry.Values)
        {
            var compType = reg.Component.GetType();
            if (remove)
                RemComp(ent, compType);
            else
            {
                if (HasComp(ent, compType))
                    continue;
                AddComp(ent, _compFactory.GetComponent(compType));
            }
        }
    }

    private bool TryToolCheck(ComponentRegistry? components, EntityUid target, bool checkMissing = true)
    {
        if (components == null)
            return false;

        foreach (var (_, entry) in components)
        {
            var hasComponent = HasComp(target, entry.Component.GetType());
            if (checkMissing != hasComponent)
                return true; // Early exit if condition fails
        }

        return false;
    }

    private bool TryDoSurgeryStep(EntityUid body, EntityUid targetPart, EntityUid user, EntProtoId surgeryId, EntProtoId stepId)
        => TryDoSurgeryStep(body, targetPart, user, surgeryId, stepId, out _);

    /// <summary>
    /// Do a surgery step on a part, if it can be done.
    /// Returns true if it succeeded.
    /// </summary>
    public bool TryDoSurgeryStep(EntityUid body, EntityUid targetPart, EntityUid user, EntProtoId surgeryId, EntProtoId stepId, out StepInvalidReason error)
    {
        error = StepInvalidReason.None;
        if (!IsSurgeryValid(body, targetPart, surgeryId, stepId, user, out var surgery, out var part, out var step))
        {
            error = StepInvalidReason.SurgeryInvalid;
            return false;
        }

        if (!PreviousStepsComplete(body, part, surgery, stepId, user))
        {
            error = StepInvalidReason.MissingPreviousSteps;
            return false;
        }

        if (IsStepComplete(body, part, stepId, surgery))
        {
            error = StepInvalidReason.StepCompleted;
            return false;
        }

        var tool = _hands.GetActiveItemOrSelf(user);
        if (!CanPerformStep(user, body, part, step, tool, true, out _, out error, out var data))
            return false;

        var toolComp = _toolQuery.CompOrNull(tool);
        var usedEv = new SurgeryToolUsedEvent(user, body);
        usedEv.IgnoreToggle = toolComp?.IgnoreToggle ?? false;
        RaiseLocalEvent(tool, ref usedEv);
        if (usedEv.Cancelled)
        {
            error = StepInvalidReason.ToolInvalid;
            return false;
        }

        if (toolComp?.StartSound is { } sound)
            _audio.PlayPvs(sound, tool);

        _rotateToFace.TryFaceCoordinates(user, _transform.GetMapCoordinates(body).Position);

        // We need to check for nullability because of surgeries that dont require a tool, like Cavity Implants
        var speed = data?.Speed ?? 1f;
        var toolUsed = data?.Used ?? false; // if no tool is being used you can't consume it
        var ev = new SurgeryDoAfterEvent(surgeryId, stepId, toolUsed);
        var duration = GetSurgeryDuration(step, user, body, speed);

        if (TryComp(user, out SurgerySpeedModifierComponent? surgerySpeedMod))
            duration = duration / surgerySpeedMod.SpeedModifier;

        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(duration), ev, body, part)
        {
            BreakOnMove = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.All,
            NeedHand = true,
            BreakOnHandChange = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            error = StepInvalidReason.DoAfterFailed;
            return false;
        }

        var userName = Identity.Entity(user, EntityManager);
        var targetName = Identity.Entity(body, EntityManager);

        var locName = $"surgery-popup-procedure-{surgeryId}-step-{stepId}";
        var locResult = Loc.GetString(locName,
            ("user", userName), ("target", targetName), ("part", part));

        if (locResult == locName)
            locResult = Loc.GetString($"surgery-popup-step-{stepId}",
                ("user", userName), ("target", targetName), ("part", part));

        _popup.PopupPredicted(locResult, user, user);
        return true;
    }

    private float GetSurgeryDuration(EntityUid surgeryStep, EntityUid user, EntityUid target, float toolSpeed)
    {
        if (!_stepQuery.TryComp(surgeryStep, out var stepComp))
            return 2f; // Shouldnt really happen but just a failsafe.

        var speed = toolSpeed;
        if (TryComp<BuckleComponent>(target, out var buckleComp)) // Get buckle component from target.
            if (TryComp<OperatingTableComponent>(buckleComp.BuckledTo, out var operatingTableComponent)) // If they are buckled to entity with operating table component
                speed *= operatingTableComponent.SpeedModifier; // apply surgery speed modifier
        if (TryComp(user, out SurgerySpeedModifierComponent? surgerySpeedMod))
            speed *= surgerySpeedMod.SpeedModifier;

        return stepComp.Duration / speed;
    }

    private (Entity<SurgeryComponent> Surgery, int Step)? GetNextStep(EntityUid body, EntityUid part, Entity<SurgeryComponent?> surgery, List<EntityUid> requirements, EntityUid user)
    {
        if (!Resolve(surgery, ref surgery.Comp))
            return null;

        if (requirements.Contains(surgery))
            throw new ArgumentException($"Surgery {surgery} has a requirement loop: {string.Join(", ", requirements)}");

        var ev = new SurgeryIgnorePreviousStepsEvent();
        RaiseLocalEvent(user, ev);
        if (ev.Handled)
        {
            for (var i = surgery.Comp.Steps.Count - 1; i >= 0; i--)
            {
                var surgeryStep = surgery.Comp.Steps[i];
                if (!IsStepComplete(body, part, surgeryStep, surgery))
                    return ((surgery, surgery.Comp), -i - 1);
            }

            return null;
        }

        requirements.Add(surgery);

        if (surgery.Comp.Requirement is { } requirementId &&
            GetSingleton(requirementId) is { } requirement &&
            GetNextStep(body, part, requirement, requirements, user) is { } requiredNext)
        {
            return requiredNext;
        }

        for (var i = 0; i < surgery.Comp.Steps.Count; i++)
        {
            var surgeryStep = surgery.Comp.Steps[i];
            if (!IsStepComplete(body, part, surgeryStep, surgery))
                return ((surgery, surgery.Comp), i);
        }

        return null;
    }

    public (Entity<SurgeryComponent> Surgery, int Step)? GetNextStep(EntityUid body, EntityUid part, EntityUid surgery, EntityUid user)
    {
        _nextStepList.Clear();
        return GetNextStep(body, part, surgery, _nextStepList, user);
    }

    private bool PreviousStepsComplete(EntityUid body, EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId step, EntityUid user)
    {
        var ev = new SurgeryIgnorePreviousStepsEvent();
        RaiseLocalEvent(user, ev);
        if (ev.Handled)
            return true;

        if (surgery.Comp.Requirement is { } requirement)
        {
            if (GetSingleton(requirement) is not { } requiredEnt ||
                !TryComp(requiredEnt, out SurgeryComponent? requiredComp) ||
                !PreviousStepsComplete(body, part, (requiredEnt, requiredComp), step, user))
                return false;
        }

        foreach (var surgeryStep in surgery.Comp.Steps)
        {
            if (surgeryStep == step)
                return true;

            if (!IsStepComplete(body, part, surgeryStep, surgery))
                return false;
        }

        return true;
    }

    private bool CanPerformStep(EntityUid user,
        EntityUid body,
        EntityUid part,
        EntityUid step,
        EntityUid tool,
        bool doPopup,
        out string? popup,
        out StepInvalidReason reason,
        out ISurgeryToolComponent? data)
    {
        data = null;

        var category = _organQuery.CompOrNull(part)?.Category;

        var slot = category?.Id switch
        {
            "Head" => SlotFlags.HEAD,
            "Torso" => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            "ArmLeft" or "ArmRight" => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            "HandLeft" or "HandRight" => SlotFlags.GLOVES,
            "LegLeft" or "LegRight" => SlotFlags.OUTERCLOTHING | SlotFlags.LEGS,
            "FootLeft" or "FootRight" => SlotFlags.FEET,
            _ => SlotFlags.NONE,
        };

        var check = new SurgeryCanPerformStepEvent(user, body, tool, slot);
        RaiseLocalEvent(step, ref check);
        if (check.IsValid) // if the step doesn't stop it check the body after
            RaiseLocalEvent(body, ref check);

        popup = check.Popup;
        reason = check.Invalid;
        data = check.ValidTool;

        if (check.IsValid)
            return true;

        if (doPopup && check.Popup != null)
            _popup.PopupClient(check.Popup, user, user, PopupType.SmallCaution);

        return false;
    }

    private bool CanPerformStep(EntityUid user, EntityUid body, EntityUid part, EntityUid step, EntityUid tool, bool doPopup)
    {
        return CanPerformStep(user, body, part, step, tool, doPopup, out _, out _, out _);
    }

    public bool CanPerformStepWithHeld(EntityUid user, EntityUid body, EntityUid part, EntityUid step, bool doPopup, out string? popup)
    {
        var tool = _hands.GetActiveItemOrSelf(user);
        return CanPerformStep(user, body, part, step, tool, doPopup, out popup, out _, out _);
    }

    private bool IsStepComplete(EntityUid body, EntityUid part, EntProtoId step, EntityUid surgery)
    {
        if (GetSingleton(step) is not { } stepEnt)
            return false;

        var ev = new SurgeryStepCompleteCheckEvent(body, part, surgery);
        RaiseLocalEvent(stepEnt, ref ev);
        return !ev.Cancelled;
    }

    private ISurgeryToolComponent? GetSurgeryComp(EntityUid tool, IComponent component)
    {
        if (TryComp(tool, component.GetType(), out var found) && found is ISurgeryToolComponent data)
            return data;

        return null;
    }

    private bool HasSurgeryComp(EntityUid tool, IComponent component) => GetSurgeryComp(tool, component) != null;

    #endregion
}

using System.Linq;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Administration.Logs;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Medical.Healing;

public sealed partial class HealingSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private WoundSystem _wound = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealingComponent, UseInHandEvent>(OnHealingUse);
        SubscribeLocalEvent<HealingComponent, AfterInteractEvent>(OnHealingAfterInteract);
        SubscribeLocalEvent<DamageableComponent, HealingDoAfterEvent>(OnDoAfter);
    }

    private void OnDoAfter(Entity<DamageableComponent> target, ref HealingDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp(args.Used, out HealingComponent? healing))
            return;

        if (!TryComp<InjurableComponent>(target, out var injurable))
            return;

        if (healing.DamageContainers is not null &&
            injurable.DamageContainer is not null &&
            !healing.DamageContainers.Contains(injurable.DamageContainer.Value))
        {
            return;
        }

        TryComp<BloodstreamComponent>(target, out var bloodstream);
        DamageSpecifier healed;

        var hasOrgan = TryResolveTargetedOrgan(args.User, target.Owner, out var organ, out var woundable);

        // Heal some bloodloss damage.
        if (healing.BloodlossModifier != 0)
        {
            if (hasOrgan)
            {
                var wasBleeding = woundable!.Bleeds > FixedPoint2.Zero;
                _wound.TryHealBleedingWounds(organ, FixedPoint2.New(-healing.BloodlossModifier), out _, woundable);
                if (wasBleeding && woundable.Bleeds <= FixedPoint2.Zero)
                {
                    var popup = (args.User == target.Owner)
                        ? Loc.GetString("medical-item-stop-bleeding-self")
                        : Loc.GetString("medical-item-stop-bleeding", ("target", Identity.Entity(target.Owner, EntityManager)));
                    _popupSystem.PopupClient(popup, target, args.User);
                }
            }
            else if (bloodstream != null)
            {
                var isBleeding = bloodstream.BleedAmount > 0;
                _bloodstreamSystem.TryModifyBleedAmount((target.Owner, bloodstream), healing.BloodlossModifier);
                if (isBleeding != bloodstream.BleedAmount > 0)
                {
                    var popup = (args.User == target.Owner)
                        ? Loc.GetString("medical-item-stop-bleeding-self")
                        : Loc.GetString("medical-item-stop-bleeding", ("target", Identity.Entity(target.Owner, EntityManager)));
                    _popupSystem.PopupClient(popup, target, args.User);
                }
            }
        }

        // Restores missing blood
        if (healing.ModifyBloodLevel != 0 && bloodstream != null)
            _bloodstreamSystem.TryModifyBloodLevel((target.Owner, bloodstream), healing.ModifyBloodLevel);

        if (hasOrgan && TraumaSystem.TraumasBlockingHealing.Any(t => _trauma.HasWoundableTrauma(organ, t, woundable, showAll: false)))
        {
            _popupSystem.PopupClient(Loc.GetString("medical-item-requires-surgery", ("target", Identity.Entity(target.Owner, EntityManager))), target, args.User);

            if (healing.BloodlossModifier == 0)
                return;

            healed = new DamageSpecifier();
        }
        else
        {
            var scaled = healing.Damage * _damageable.UniversalTopicalsHealModifier;

            if (hasOrgan)
            {
                var actualOrganHeal = new DamageSpecifier(scaled);
                var mobOnlyHeal = new DamageSpecifier(scaled);
                foreach (var (type, amount) in scaled.DamageDict)
                {
                    if (amount >= 0)
                    {
                        // A positive (damage-dealing) entry mixed into an otherwise-healing
                        // DamageSpecifier (a "heals X but deals a bit of Y" tradeoff item) -
                        // actualOrganHeal/mobOnlyHeal both start as full copies of `scaled`, so
                        // leaving this entry untouched in both applied it to the organ AND
                        // directly to the mob, double-counting it (the organ side already
                        // reaches the mob on its own via BodyDamageBridgeSystem's auto-sync).
                        // Zero it out of the mob-only copy so it only applies once.
                        mobOnlyHeal.DamageDict[type] = FixedPoint2.Zero;
                        continue;
                    }

                    if (_wound.HasDamageOfType(organ, type, woundable))
                    {
                        actualOrganHeal.DamageDict[type] = -_wound.GetHealableSeverity(organ, type, -amount, woundable);
                        mobOnlyHeal.DamageDict[type] = FixedPoint2.Zero;
                    }
                    else if (_wound.GetTypeDamage(organ, type) is var rawOrganDamage && rawOrganDamage > 0)
                    {
                        // The targeted organ has real damage of this type that's too small to
                        // have ever formed a wound (TryCreateWound's minorThreshold) - heal it
                        // directly on the organ. Leaving this on mobOnlyHeal below would've
                        // drained the mob's aggregate pool instead, which in practice heals
                        // whichever OTHER organ actually holds a wound of this type - the
                        // targeted organ's own damage never moves, and once that other organ
                        // empties the pool, HasDamage's pre-use gate refuses the item entirely
                        // even though the targeted organ is still genuinely damaged.
                        actualOrganHeal.DamageDict[type] = -FixedPoint2.Min(-amount, rawOrganDamage);
                        mobOnlyHeal.DamageDict[type] = FixedPoint2.Zero;
                    }
                    else
                    {
                        // Targeted organ has no wound and no raw damage of this type at all -
                        // nothing on this organ to heal. Leaving mobOnlyHeal at its full nominal
                        // `scaled` value here would drain the mob's aggregate pool unclamped and
                        // ungated even though the targeted part is uninjured, desyncing mob-total
                        // from organ-total the same way the wound/raw-damage branches above guard
                        // against, and in practice healing whatever OTHER limb holds a wound of
                        // this type instead of the one actually targeted.
                        actualOrganHeal.DamageDict[type] = FixedPoint2.Zero;
                        mobOnlyHeal.DamageDict[type] = FixedPoint2.Zero;
                    }
                }

                _damageable.TryChangeDamage(organ, actualOrganHeal, out var organHealed, true, origin: args.Args.User);
                if (!_damageable.TryChangeDamage(target.Owner, mobOnlyHeal, out var mobHealed, true, origin: args.Args.User) && healing.BloodlossModifier != 0 && organHealed.Empty)
                    return;

                healed = organHealed + mobHealed;
            }
            else
            {
                // No organ to heal through (mob-only damage sources like Barotrauma/Temperature,
                // or simple mobs without wound support) - heal the mob's pool directly.
                if (!_damageable.TryChangeDamage(target.Owner, scaled, out healed, true, origin: args.Args.User) && healing.BloodlossModifier != 0)
                    return;
            }
        }

        var total = healed.GetTotal();

        // Re-verify that we can heal the damage.
        var dontRepeat = false;
        if (TryComp<StackComponent>(args.Used.Value, out var stackComp))
        {
            _stacks.ReduceCount((args.Used.Value, stackComp), 1);

            if (_stacks.GetCount((args.Used.Value, stackComp)) <= 0)
                dontRepeat = true;
        }
        else
        {
            PredictedQueueDel(args.Used.Value);
        }

        if (target.Owner != args.User)
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed {ToPrettyString(target.Owner):target} for {total:damage} damage");
        }
        else
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed themselves for {total:damage} damage");
        }

        _audio.PlayPredicted(healing.HealingEndSound, target.Owner, args.User);

        // Logic to determine the whether or not to repeat the healing action
        args.Repeat = HasDamage((args.Used.Value, healing), target, args.User) && !dontRepeat;
        args.Handled = true;

        if (!args.Repeat)
        {
            _popupSystem.PopupEntity(Loc.GetString("medical-item-finished-using", ("item", args.Used)), target.Owner, args.User);
            return;
        }

        // Update our self heal delay so it shortens as we heal more damage.
        if (args.User == target.Owner)
            args.Args.Delay = healing.Delay * GetScaledHealingPenalty(target.Owner, healing.SelfHealPenaltyMultiplier);
    }

    private bool TryResolveTargetedOrgan(EntityUid healer, EntityUid patient, out EntityUid organ, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out WoundableComponent? woundable)
    {
        organ = default;
        woundable = null;

        if (!TryComp<BodyComponent>(patient, out var body) || body.Organs is null)
            return false;

        if (!TryComp<TargetingComponent>(healer, out var targeting)
            || !LimbTargetMap.TryGetCategory(targeting.Target, out var category)
            || !LimbTargetMap.TryGetOrganByCategory(EntityManager, body, category, out var resolvedOrgan))
        {
            return false;
        }

        if (!TryComp(resolvedOrgan, out woundable))
            return false;

        organ = resolvedOrgan;
        return true;
    }

    private bool HasDamage(Entity<HealingComponent> healing, Entity<DamageableComponent> target, EntityUid user)
    {
        var healingDict = healing.Comp.Damage.DamageDict;

        if (TryResolveTargetedOrgan(user, target.Owner, out var organ, out _))
        {
            foreach (var type in healingDict)
            {
                if (_wound.GetTypeDamage(organ, type.Key) > 0)
                    return true;
            }
        }
        else
        {
            var damageableDict = _damageable.GetAllDamage(target.AsNullable()).DamageDict;
            foreach (var type in healingDict)
            {
                if (damageableDict.TryGetValue(type.Key, out var amount) && amount > 0)
                {
                    return true;
                }
            }
        }

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            // Is ent missing blood that we can restore?
            if (healing.Comp.ModifyBloodLevel > 0
                && _solutionContainerSystem.ResolveSolution(target.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution)
                && _bloodstreamSystem.GetBloodLevel((target, bloodstream)) < 1)
            {
                return true;
            }

            // Is ent bleeding and can we stop it?
            if (healing.Comp.BloodlossModifier < 0 && bloodstream.BleedAmount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void OnHealingUse(Entity<HealingComponent> healing, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryHeal(healing, args.User, args.User))
            args.Handled = true;
    }

    private void OnHealingAfterInteract(Entity<HealingComponent> healing, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (TryHeal(healing, args.Target.Value, args.User))
            args.Handled = true;
    }

    private bool TryHeal(Entity<HealingComponent> healing, Entity<DamageableComponent?> target, EntityUid user)
    {
        if (!Resolve(target, ref target.Comp, false))
            return false;

        if (!TryComp<InjurableComponent>(target, out var injurable))
            return false;

        if (healing.Comp.DamageContainers is not null &&
            injurable.DamageContainer is not null &&
            !healing.Comp.DamageContainers.Contains(injurable.DamageContainer.Value))
        {
            return false;
        }

        if (user != target.Owner && !_interactionSystem.InRangeUnobstructed(user, target.Owner, popup: true))
            return false;

        if (TryComp<StackComponent>(healing, out var stack) && stack.Count < 1)
            return false;

        if (!HasDamage(healing, target!, user))
        {
            _popupSystem.PopupClient(Loc.GetString("medical-item-cant-use", ("item", healing.Owner)), healing, user);
            return false;
        }

        _audio.PlayPredicted(healing.Comp.HealingBeginSound, healing, user);

        var isNotSelf = user != target.Owner;

        if (isNotSelf)
        {
            var msg = Loc.GetString("medical-item-popup-target", ("user", Identity.Entity(user, EntityManager)), ("item", healing.Owner));
            _popupSystem.PopupEntity(msg, target, target, PopupType.Medium);
        }

        var delay = isNotSelf
            ? healing.Comp.Delay
            : healing.Comp.Delay * GetScaledHealingPenalty(target, healing.Comp.SelfHealPenaltyMultiplier);

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, delay, new HealingDoAfterEvent(), target, target: target, used: healing)
            {
                // Didn't break on damage as they may be trying to prevent it and
                // not being able to heal your own ticking damage would be frustrating.
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
        return true;
    }

    /// <summary>
    /// Scales the self-heal penalty based on the amount of damage taken
    /// </summary>
    /// <param name="ent">Entity we're healing</param>
    /// <param name="mod">Maximum modifier we can have.</param>
    /// <returns>Modifier we multiply our healing time by</returns>
    public float GetScaledHealingPenalty(Entity<DamageableComponent?, MobThresholdsComponent?> ent, float mod)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, false))
            return mod;

        if (!_mobThresholdSystem.TryGetThresholdForState(ent, MobState.Critical, out var amount, ent.Comp2))
            return 1;

        var percentDamage = (float)(_damageable.GetTotalDamage(ent) / amount);
        //basically make it scale from 1 to the multiplier.

        var output = percentDamage * (mod - 1) + 1;
        return Math.Max(output, 1);
    }
}

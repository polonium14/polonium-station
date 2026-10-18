using Content.Server.Destructible;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Actions;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorEvolutionChamberSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> HullDamageType = "Blunt";

    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ReplicatorHiveSystem _hive = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private ReplicatorModuleSystem _modules = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorModulesComponent, MapInitEvent>(OnModulesMapInit);
        SubscribeLocalEvent<ReplicatorEvolutionChamberComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<ReplicatorEvolutionChamberComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<ReplicatorEvolutionChamberComponent, ReplicatorEvolutionSpecializeMessage>(OnSpecializeMessage);
        SubscribeLocalEvent<ReplicatorEvolutionChamberComponent, ReplicatorSpecializeDoAfterEvent>(OnSpecializeDoAfter);
        SubscribeLocalEvent<ReplicatorEvolutionChamberComponent, ReplicatorEvolutionGrowMessage>(OnGrowMessage);
        SubscribeLocalEvent<ReplicatorEvolutionChamberComponent, ReplicatorModuleInstallDoAfterEvent>(OnInstallDoAfter);
    }

    private void OnModulesMapInit(Entity<ReplicatorModulesComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<MobThresholdsComponent>(ent, out var thresholds)
            && _mobThreshold.TryGetThresholdForState(ent, MobState.Dead, out var deadAt, thresholds))
            UpdateDestruction(ent, deadAt.Value);
    }

    private void OnUiOpenAttempt(Entity<ReplicatorEvolutionChamberComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<ReplicatorModulesComponent>(args.User))
            args.Cancel();
    }

    private void OnBeforeUiOpen(Entity<ReplicatorEvolutionChamberComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateUi(ent, args.User);
    }

    private void UpdateUi(EntityUid chamber, EntityUid user)
    {
        var state = _hive.TryGetHive(user, out var hive)
            ? new ReplicatorEvolutionState(_hive.GetBudget(hive), hive.Comp2.CurrentLevel)
            : new ReplicatorEvolutionState(0, 0);

        _ui.SetUiState(chamber, ReplicatorEvolutionUiKey.Key, state);
    }

    private string? GetClassBlocker(Entity<ReplicatorModulesComponent> user, ReplicatorClassPrototype prototype)
    {
        if (user.Comp.Class != null)
            return Loc.GetString("replicator-class-fail-chosen");

        if (!_hive.TryGetHive(user, out var hive))
            return Loc.GetString("replicator-construction-fail-no-nest");

        if (!_hive.CanAfford(hive, prototype.Cost))
            return Loc.GetString("replicator-construction-fail-budget", ("cost", ReplicatorHiveSystem.ToSheets(prototype.Cost)));

        return null;
    }

    private void OnSpecializeMessage(Entity<ReplicatorEvolutionChamberComponent> ent, ref ReplicatorEvolutionSpecializeMessage args)
    {
        var user = args.Actor;
        if (!TryComp<ReplicatorModulesComponent>(user, out var modules)
            || !_mobState.IsAlive(user)
            || !_prototype.TryIndex(args.Class, out var prototype))
            return;

        if (GetClassBlocker((user, modules), prototype) is { } blocker)
        {
            _popup.PopupEntity(blocker, user, user, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.InstallTime, new ReplicatorSpecializeDoAfterEvent(prototype.ID), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("replicator-module-started"), user, user);
    }

    private void OnSpecializeDoAfter(Entity<ReplicatorEvolutionChamberComponent> ent, ref ReplicatorSpecializeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !TryComp<ReplicatorModulesComponent>(args.User, out var modules))
            return;

        args.Handled = true;

        var prototype = _prototype.Index(args.Class);
        if (GetClassBlocker((args.User, modules), prototype) is { } blocker)
        {
            _popup.PopupEntity(blocker, args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (!_hive.TryGetHive(args.User, out var hive) || !_hive.TrySpend(hive, prototype.Cost))
            return;

        Specialize((args.User, modules), prototype);

        _audio.PlayPvs(ent.Comp.InstallSound, ent);
        _popup.PopupEntity(Loc.GetString("replicator-class-installed", ("class", Loc.GetString(prototype.Name))),
            args.User, args.User, PopupType.Medium);

        UpdateUi(ent, args.User);
    }

    public void Specialize(Entity<ReplicatorModulesComponent> ent, ReplicatorClassPrototype prototype)
    {
        ent.Comp.Class = prototype.ID;
        Dirty(ent);
        EntityManager.AddComponents(ent, prototype.Components);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        UpdateHealth(ent);
    }

    private void UpdateHealth(Entity<ReplicatorModulesComponent> ent)
    {
        if (!_modules.TryGetClass(ent.AsNullable(), out var prototype)
            || prototype.GetForm(_modules.GetTotalStages(ent.Comp)) is not { Health: { } health })
            return;

        _mobThreshold.SetMobStateThreshold(ent, health, MobState.Dead);
        UpdateDestruction(ent, health);
    }

    private void UpdateDestruction(Entity<ReplicatorModulesComponent> ent, FixedPoint2 health)
    {
        if (!TryComp<DestructibleComponent>(ent, out var destructible))
            return;

        foreach (var threshold in destructible.Thresholds)
        {
            switch (threshold.Trigger)
            {
                case DamageTypeTrigger trigger when trigger.DamageType == HullDamageType:
                    trigger.Damage = health * ent.Comp.HullBreakMultiplier;
                    break;
                case DamageTrigger trigger:
                    trigger.Damage = health * ent.Comp.OverkillMultiplier;
                    break;
            }
        }
    }

    private string? GetBlocker(Entity<ReplicatorModulesComponent> user, ReplicatorModulePrototype module)
    {
        if (!_modules.TryGetClass(user.AsNullable(), out var prototype) || !prototype.Modules.Contains(module.ID))
            return Loc.GetString("replicator-module-fail-class");

        var current = _modules.GetStage(user.AsNullable(), module.ID);
        if (current >= module.Stages.Count)
            return Loc.GetString("replicator-module-fail-complete");

        if (_modules.GetTotalStages(user.Comp) >= user.Comp.MaxStages)
            return Loc.GetString("replicator-module-fail-full", ("limit", user.Comp.MaxStages));

        if (!_hive.TryGetHive(user, out var hive))
            return Loc.GetString("replicator-construction-fail-no-nest");

        var stage = module.Stages[current];
        if (hive.Comp2.CurrentLevel < stage.MinNestLevel)
            return Loc.GetString("replicator-build-fail-level", ("level", stage.MinNestLevel));

        if (!_hive.CanAfford(hive, stage.Cost))
            return Loc.GetString("replicator-construction-fail-budget", ("cost", ReplicatorHiveSystem.ToSheets(stage.Cost)));

        return null;
    }

    private void OnGrowMessage(Entity<ReplicatorEvolutionChamberComponent> ent, ref ReplicatorEvolutionGrowMessage args)
    {
        var user = args.Actor;
        if (!TryComp<ReplicatorModulesComponent>(user, out var modules)
            || !_mobState.IsAlive(user)
            || !_prototype.TryIndex(args.Module, out var module))
            return;

        if (GetBlocker((user, modules), module) is { } blocker)
        {
            _popup.PopupEntity(blocker, user, user, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.InstallTime, new ReplicatorModuleInstallDoAfterEvent(module.ID), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("replicator-module-started"), user, user);
    }

    private void OnInstallDoAfter(Entity<ReplicatorEvolutionChamberComponent> ent, ref ReplicatorModuleInstallDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !TryComp<ReplicatorModulesComponent>(args.User, out var modules))
            return;

        args.Handled = true;

        var module = _prototype.Index(args.Module);
        if (GetBlocker((args.User, modules), module) is { } blocker)
        {
            _popup.PopupEntity(blocker, args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var stage = module.Stages[_modules.GetStage((args.User, modules), module.ID)];
        if (!_hive.TryGetHive(args.User, out var hive) || !_hive.TrySpend(hive, stage.Cost))
            return;

        Grow((args.User, modules), module);
        _audio.PlayPvs(ent.Comp.InstallSound, ent);
        _popup.PopupEntity(Loc.GetString("replicator-module-installed",
                ("module", Loc.GetString(module.Name)),
                ("stage", _modules.GetStage((args.User, modules), module.ID))),
            args.User, args.User, PopupType.Medium);

        UpdateUi(ent, args.User);
    }

    public void Grow(Entity<ReplicatorModulesComponent> ent, ReplicatorModulePrototype module)
    {
        var number = _modules.GetStage(ent.AsNullable(), module.ID) + 1;
        if (number > module.Stages.Count)
            return;

        var stage = module.Stages[number - 1];
        ent.Comp.Stages[module.ID] = number;
        Dirty(ent);

        EntityManager.AddComponents(ent, stage.Components);

        for (var i = 0; i < stage.Items.Count; i++)
        {
            var hand = $"module-{module.ID}-{number}-{i}";
            _hands.AddHand(ent.Owner, hand, HandLocation.Middle);
            _hands.TryPickup(ent, Spawn(stage.Items[i], Transform(ent).Coordinates), hand, checkActionBlocker: false, animate: false);
        }

        if (stage.SheetHands > 0
            && TryComp<ReplicatorSheetHandComponent>(ent, out var sheetHand)
            && _hands.TryGetHand(ent.Owner, sheetHand.HandId, out var template))
        {
            for (var i = 0; i < stage.SheetHands; i++)
            {
                var hand = $"{sheetHand.HandId}-{module.ID}-{number}-{i}";
                _hands.AddHand(ent.Owner, hand, template.Value);
                sheetHand.ExtraHands.Add(hand);
            }
        }

        foreach (var action in stage.Actions)
        {
            _actions.AddAction(ent, action);
        }

        if (TryComp<ReplicatorComponent>(ent, out var replicator)
            && _hands.TryGetHeldItem(ent.Owner, replicator.StunRayHandId, out var gun))
        {
            var boost = EnsureComp<ReplicatorGunBoostComponent>(gun.Value);
            boost.FireRateMultiplier = _modules.GetFireRateMultiplier(ent);
            Dirty(gun.Value, boost);
            _gun.RefreshModifiers(gun.Value);
            BoostAmmo((gun.Value, boost), _modules.GetGunCapacityBonus(ent), _modules.GetGunRechargeMultiplier(ent));
        }

        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        _movementSpeed.RefreshWeightlessModifiers(ent.Owner);
        UpdateHealth(ent);
    }

    private void BoostAmmo(Entity<ReplicatorGunBoostComponent> gun, int capacityBonus, float rechargeMultiplier)
    {
        if (TryComp<BasicEntityAmmoProviderComponent>(gun, out var ammo) && ammo.Capacity is { } capacity)
        {
            gun.Comp.BaseCapacity ??= capacity;

            var boosted = gun.Comp.BaseCapacity.Value + capacityBonus;
            if (boosted != capacity)
            {
                ammo.Capacity = boosted;
                _gun.UpdateBasicEntityAmmoCount((gun.Owner, ammo), boosted);
            }
        }

        if (!TryComp<RechargeBasicEntityAmmoComponent>(gun, out var recharge))
            return;

        gun.Comp.BaseRechargeCooldown ??= recharge.RechargeCooldown;
        recharge.RechargeCooldown = gun.Comp.BaseRechargeCooldown.Value * rechargeMultiplier;
        Dirty(gun.Owner, recharge);
    }
}

using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Horror;

public sealed partial class EldritchHorrorSystem
{
    [Dependency] private MobThresholdSystem _thresholds = default!;

    private void InitializeMutations()
    {
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorMutateActionEvent>(OnMutate);
        SubscribeLocalEvent<EldritchHorrorComponent, HorrorBuyMutationMessage>(OnBuyMutation);
        SubscribeLocalEvent<EldritchHorrorComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMutate(Entity<EldritchHorrorComponent> ent, ref HorrorMutateActionEvent args)
    {
        if (args.Handled)
            return;

        _popup.PopupEntity(Loc.GetString("horror-mutate-open"), ent, ent);
        _ui.TryToggleUi(ent.Owner, HorrorMutationsUiKey.Key, ent);
        UpdateMutationsUi(ent);
        args.Handled = true;
    }

    private void UpdateMutationsUi(Entity<EldritchHorrorComponent> ent)
    {
        if (_ui.IsUiOpen(ent.Owner, HorrorMutationsUiKey.Key))
        {
            var owned = new List<string>();
            foreach (var ability in ent.Comp.OwnedAbilities)
                owned.Add(ability);
            foreach (var upgrade in ent.Comp.Upgrades)
                owned.Add(upgrade);

            _ui.SetUiState(ent.Owner, HorrorMutationsUiKey.Key, new HorrorMutationsBoundUserInterfaceState(ent.Comp.SoulPoints, ent.Comp.ConsumedSouls, owned));
        }
    }

    private void OnBuyMutation(Entity<EldritchHorrorComponent> ent, ref HorrorBuyMutationMessage args)
    {
        if (args.Actor != ent.Owner)
            return;

        var bought = args.Upgrade ? TryBuyUpgrade(ent, args.Id) : TryBuyAbility(ent, args.Id);
        if (bought)
            UpdateMutationsUi(ent);
    }

    /// <summary>
    /// Spends souls on an ability listed in the mutations menu.
    /// </summary>
    public bool TryBuyAbility(Entity<EldritchHorrorComponent> ent, string id)
    {
        if (!Proto.TryIndex<EntityPrototype>(id, out var proto)
            || !proto.TryComp<HorrorActionComponent>(out var action, Factory)
            || action.SoulPrice <= 0
            || ent.Comp.OwnedAbilities.Contains(proto.ID)
            || ent.Comp.SoulPoints < action.SoulPrice)
            return false;

        ent.Comp.SoulPoints -= action.SoulPrice;
        ent.Comp.OwnedAbilities.Add(proto.ID);
        Dirty(ent);
        RefreshActions(ent);

        _popup.PopupEntity(Loc.GetString("horror-mutate-ability", ("name", proto.Name)), ent, ent, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(ent):horror} mutated the ability {proto.ID}");
        return true;
    }

    /// <summary>
    /// Spends souls on a permanent upgrade.
    /// </summary>
    public bool TryBuyUpgrade(Entity<EldritchHorrorComponent> ent, string id)
    {
        if (!Proto.TryIndex<HorrorUpgradePrototype>(id, out var upgrade)
            || ent.Comp.Upgrades.Contains(upgrade.ID)
            || ent.Comp.SoulPoints < upgrade.SoulPrice)
            return false;

        ent.Comp.SoulPoints -= upgrade.SoulPrice;
        ent.Comp.Upgrades.Add(upgrade.ID);
        Dirty(ent);
        ApplyUpgrade(ent, upgrade);
        UpdateChemicalsUi(ent);

        _popup.PopupEntity(Loc.GetString("horror-mutate-upgrade", ("name", Loc.GetString(upgrade.Name))), ent, ent, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(ent):horror} took the upgrade {upgrade.ID}");
        return true;
    }

    /// <summary>
    /// One-time effects of an upgrade. The rest is read from the owned upgrades when it matters.
    /// </summary>
    private void ApplyUpgrade(Entity<EldritchHorrorComponent> ent, HorrorUpgradePrototype upgrade)
    {
        if (!MathHelper.CloseTo(upgrade.ThresholdMultiplier, 1f)
            && _thresholds.TryGetThresholdForState(ent, MobState.Dead, out var dead))
        {
            _thresholds.SetMobStateThreshold(ent, dead.Value * upgrade.ThresholdMultiplier, MobState.Dead);
        }

        if (upgrade.BiteBonus > 0 && TryComp<MeleeWeaponComponent>(ent, out var melee))
        {
            foreach (var type in new List<ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype>>(melee.Damage.DamageDict.Keys))
            {
                melee.Damage.DamageDict[type] += upgrade.BiteBonus;
                break;
            }

            Dirty(ent, melee);
        }
    }

    /// <summary>
    /// Serrated teeth tear through cyborg plating even better.
    /// </summary>
    private void OnMeleeHit(Entity<EldritchHorrorComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.Weapon != ent.Owner)
            return;

        // biting someone drops the disguises
        var reveal = new HorrorRevealEvent();
        RaiseLocalEvent(ent, ref reveal);

        var bonus = FixedPoint2Sum(ent, static upgrade => upgrade.SiliconBiteBonus);
        if (bonus <= 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!HasComp<BorgChassisComponent>(hit))
                continue;

            foreach (var type in args.BaseDamage.DamageDict.Keys)
            {
                args.BonusDamage += new DamageSpecifier(Proto.Index<Content.Shared.Damage.Prototypes.DamageTypePrototype>(type), bonus);
                break;
            }

            break;
        }
    }

    private Content.Shared.FixedPoint.FixedPoint2 FixedPoint2Sum(Entity<EldritchHorrorComponent> ent,
        Func<HorrorUpgradePrototype, Content.Shared.FixedPoint.FixedPoint2> selector)
    {
        var sum = Content.Shared.FixedPoint.FixedPoint2.Zero;
        foreach (var upgrade in GetUpgrades(ent))
            sum += selector(upgrade);

        return sum;
    }
}

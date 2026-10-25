using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared.Examine;
using Content.Shared.Power;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Modules.ModSuit;

public partial class SharedModSuitSystem
{
    [Dependency] private SharedStackSystem _stack = default!;

    /// <summary>
    /// The container the construction graph puts the core into, kept when the frame becomes a suit.
    /// </summary>
    private const string CoreContainerId = "core";

    private static readonly SpriteSpecifier FuelVerbIcon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/insert.svg.192dpi.png"));

    private void InitializeCore()
    {
        SubscribeLocalEvent<ModSuitPartDeployableComponent, RefreshChargeRateEvent>(OnCoreRefreshChargeRate);
        SubscribeLocalEvent<ModSuitPartDeployableComponent, GetVerbsEvent<AlternativeVerb>>(OnCoreGetFuelVerbs);
        SubscribeLocalEvent<ModSuitPartDeployableComponent, ExaminedEvent>(OnCoreExamined);
    }

    private void OnCoreRefreshChargeRate(Entity<ModSuitPartDeployableComponent> ent, ref RefreshChargeRateEvent args)
    {
        if (GetCore(ent.Owner) is { } core)
            args.NewChargeRate += core.RechargeRate;
    }

    private void OnCoreGetFuelVerbs(Entity<ModSuitPartDeployableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } fuel)
            return;

        if (GetCore(ent.Owner) is not { FuelStack: { } fuelStack } core ||
            !TryComp<StackComponent>(fuel, out var stack) ||
            stack.StackTypeId != fuelStack)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("modsuit-core-refuel-verb"),
            Icon = FuelVerbIcon,
            // Above the cell verb, so alt-clicking with fuel in hand refuels instead of pulling the cell out.
            Priority = 1,
            Act = () => Refuel(ent.Owner, core, (fuel, stack), user),
        });
    }

    private void Refuel(EntityUid suit, ModSuitCoreComponent core, Entity<StackComponent> fuel, EntityUid user)
    {
        if (!_powerCell.TryGetBatteryFromSlot(suit, out var battery))
        {
            _popup.PopupEntity(Loc.GetString("modsuit-core-refuel-no-cell"), suit, user);
            return;
        }

        if (_battery.IsFull(battery.Value.AsNullable()))
        {
            _popup.PopupEntity(Loc.GetString("modsuit-core-refuel-full"), suit, user);
            return;
        }

        if (!_stack.TryUse(fuel.AsNullable(), 1))
            return;

        _battery.ChangeCharge(battery.Value.AsNullable(), battery.Value.Comp.MaxCharge * core.FuelCharge);
        _popup.PopupEntity(Loc.GetString("modsuit-core-refuel-done"), suit, user);
    }

    /// <summary>
    /// The special core the suit was built around, if it has one.
    /// </summary>
    [PublicAPI]
    public ModSuitCoreComponent? GetCore(EntityUid suit)
    {
        return CompOrNull<ModSuitCoreComponent>(GetCoreEntity(suit));
    }

    /// <summary>
    /// The core the suit was built around. Suits that were not built by hand have none.
    /// </summary>
    [PublicAPI]
    public EntityUid? GetCoreEntity(EntityUid suit)
    {
        if (!Container.TryGetContainer(suit, CoreContainerId, out var container) || container.ContainedEntities.Count == 0)
            return null;

        return container.ContainedEntities[0];
    }

    private void OnCoreExamined(Entity<ModSuitPartDeployableComponent> ent, ref ExaminedEvent args)
    {
        if (GetCoreEntity(ent.Owner) is { } core)
            args.PushMarkup(Loc.GetString("modsuit-core-examine", ("core", core)));
    }

    private int GetMaxComplexity(Entity<ModSuitComplexityLimitComponent> ent)
    {
        return ent.Comp.MaxComplexity + (GetCore(ent.Owner)?.ComplexityBonus ?? 0);
    }
}

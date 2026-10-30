using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using JetBrains.Annotations;

namespace Content.Shared._Polonium.Modules.ModSuit;

public partial class SharedModSuitSystem
{
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;

    private void InitializePower()
    {
        SubscribeLocalEvent<ModSuitPartDeployableComponent, PowerCellChangedEvent>(OnDeployablePowerCellChanged);
        SubscribeLocalEvent<ModSuitPartDeployableComponent, BatteryStateChangedEvent>(OnDeployableBatteryStateChanged);
    }

    private void OnDeployablePowerCellChanged(Entity<ModSuitPartDeployableComponent> ent, ref PowerCellChangedEvent args)
    {
        SetUnpoweredVisuals(ent, args.Ejected || !HasPower(ent.Owner));
    }

    private void OnDeployableBatteryStateChanged(Entity<ModSuitPartDeployableComponent> ent, ref BatteryStateChangedEvent args)
    {
        SetUnpoweredVisuals(ent, args.NewState == BatteryState.Empty);
    }

    /// <summary>
    /// Whether the suit has a power cell with any charge left.
    /// </summary>
    [PublicAPI]
    public bool HasPower(EntityUid suit)
    {
        return _powerCell.TryGetBatteryFromSlot(suit, out var battery) && _battery.GetCharge(battery.Value.AsNullable()) > 0f;
    }

    /// <summary>
    /// Whether the suit or one of its parts is drawn with its lights off.
    /// </summary>
    [PublicAPI]
    public bool IsUnpowered(EntityUid uid)
    {
        return Appearance.TryGetData<bool>(uid, ModSuitSealedVisuals.Unpowered, out var unpowered) && unpowered;
    }

    protected void SetUnpoweredVisuals(Entity<ModSuitPartDeployableComponent> ent, bool unpowered)
    {
        Appearance.SetData(ent.Owner, ModSuitSealedVisuals.Unpowered, unpowered);

        foreach (var part in GetAllParts(ent!))
            Appearance.SetData(part, ModSuitSealedVisuals.Unpowered, unpowered);

        UpdateUI(ent.Owner);
    }
}

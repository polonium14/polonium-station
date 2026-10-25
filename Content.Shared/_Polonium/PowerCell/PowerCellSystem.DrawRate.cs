using Content.Shared.PowerCell.Components;
using JetBrains.Annotations;

namespace Content.Shared.PowerCell;

public sealed partial class PowerCellSystem
{
    /// <summary>
    /// Changes how much power the entity draws per second and refreshes the battery charge rate.
    /// </summary>
    [PublicAPI]
    public void SetDrawRate(Entity<PowerCellDrawComponent?> ent, float drawRate)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.DrawRate.Equals(drawRate))
            return;

        ent.Comp.DrawRate = drawRate;
        Dirty(ent, ent.Comp);

        if (TryGetBatteryFromSlot(ent.Owner, out var battery))
            _battery.RefreshChargeRate(battery.Value.AsNullable());
    }
}

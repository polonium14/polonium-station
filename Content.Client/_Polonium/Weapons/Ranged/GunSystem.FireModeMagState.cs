using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameObjects;

// ReSharper disable once CheckNamespace
namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [SubscribeLocalEvent]
    private void OnFireModeAppearanceChange(Entity<BatteryWeaponFireModesComponent> ent, ref AppearanceChangeEvent args)
    {
        var modes = ent.Comp.FireModes;
        if (ent.Comp.CurrentFireMode < 0 || ent.Comp.CurrentFireMode >= modes.Count)
            return;

        if (modes[ent.Comp.CurrentFireMode].MagState is not { } magState
            || !TryComp<MagazineVisualsComponent>(ent, out var visuals)
            || visuals.MagState == magState)
            return;

        visuals.MagState = magState;
        Appearance.QueueUpdate(ent, args.Component);
    }
}

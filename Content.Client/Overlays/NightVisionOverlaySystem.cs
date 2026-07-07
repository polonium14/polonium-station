using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Robust.Client.Graphics;

namespace Content.Client.Overlays;

/// <summary>
/// Shows/hides the <see cref="NightVisionOverlay"/> based on whether the observed
/// entity has a <see cref="NightVisionComponent"/> equipped.
/// </summary>
public sealed partial class NightVisionOverlaySystem : EquipmentHudSystem<NightVisionComponent>
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private NightVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new NightVisionOverlay();

        SubscribeLocalEvent<NightVisionComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    protected override void OnRefreshEquipmentHud(Entity<NightVisionComponent> ent, ref InventoryRelayedEvent<RefreshEquipmentHudEvent<NightVisionComponent>> args)
    {
        // Polonium: creatures worn on the head should not grant night vision
        if (_inventory.InSlotWithAnyFlags(ent.Owner, SlotFlags.HEAD))
            return;

        base.OnRefreshEquipmentHud(ent, ref args);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<NightVisionComponent> component)
    {
        base.UpdateInternal(component);

        // Find the component with the lowest noise.
        NightVisionComponent? nvision = null;
        var bestNoise = float.MaxValue;
        foreach (var comp in component.Components)
        {
            if (!comp.Enabled)
                continue;

            var noise = comp.NoiseAmount * comp.NoiseMultiplier;
            if (noise < bestNoise)
            {
                nvision = comp;
                bestNoise = noise;
            }
        }

        // There is no active night vision components, so we disable the overlay.
        if (nvision == null)
        {
            DeactivateInternal();
            return;
        }

        _overlay.SetParameters(nvision.OverlayColor, nvision.LightingColor, nvision.NoiseAmount, nvision.NoiseMultiplier);

        if (!_overlayMan.HasOverlay<NightVisionOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnHandleState(Entity<NightVisionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshOverlay();
    }
}

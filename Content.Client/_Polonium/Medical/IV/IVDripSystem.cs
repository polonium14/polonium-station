using Content.Shared._Polonium.Medical.IV;
using Content.Shared.Rounding;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Containers;

namespace Content.Client._Polonium.Medical.IV;

public sealed partial class IVDripSystem : SharedIVDripSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        if (!_overlay.HasOverlay<IVDripOverlay>())
            _overlay.AddOverlay(new IVDripOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<IVDripOverlay>();
    }

    protected override void UpdateIVAppearance(Entity<IVDripComponent> iv)
    {
        base.UpdateIVAppearance(iv);
        if (!TryComp(iv, out SpriteComponent? sprite))
            return;

        // check if slot has an item
        bool hasBag = false;
        if (_container.TryGetContainer(iv, iv.Comp.Slot, out var container) &&
            container.ContainedEntities.Count > 0)
        {
            hasBag = true;
        }

        // determine state
        string baseState;

        if (!hasBag)
        {
            // if no bag, then show no bag
            baseState = iv.Comp.NoBagState;
        }
        else
        {
            // if yes bag, check if its attached
            baseState = iv.Comp.AttachedTo == default
                ? iv.Comp.UnattachedState
                : iv.Comp.AttachedState;
        }

        _sprite.LayerSetRsiState((iv.Owner, sprite), IVDripVisualLayers.Base, baseState);

        string? reagentState = null;
        for (var i = iv.Comp.ReagentStates.Count - 1; i >= 0; i--)
        {
            var (amount, state) = iv.Comp.ReagentStates[i];
            if (amount <= iv.Comp.FillPercentage)
            {
                reagentState = state;
                break;
            }
        }

        // if there is no bag, we force the reagent layer to hide
        if (reagentState == null || !hasBag)
        {
            _sprite.LayerSetVisible((iv.Owner, sprite), IVDripVisualLayers.Reagent, false);
            return;
        }

        _sprite.LayerSetVisible((iv.Owner, sprite), IVDripVisualLayers.Reagent, true);
        _sprite.LayerSetRsiState((iv.Owner, sprite), IVDripVisualLayers.Reagent, reagentState);
        _sprite.LayerSetColor((iv.Owner, sprite), IVDripVisualLayers.Reagent, iv.Comp.FillColor);
    }

    protected override void UpdatePackAppearance(Entity<IVBagComponent> pack)
    {
        base.UpdatePackAppearance(pack);
        if (!TryComp(pack, out SpriteComponent? sprite))
            return;

        _sprite.LayerSetVisible((pack.Owner, sprite), IVBagVisuals.Label, false);

        if (_sprite.LayerMapTryGet((pack.Owner, sprite), IVBagVisuals.Fill, out var fillLayer, false))
        {
            var fill = pack.Comp.FillPercentage.Float();
            var level = ContentHelpers.RoundToLevels(fill, 1, pack.Comp.MaxFillLevels + 1);
            var state = level > 0 ? $"{pack.Comp.FillBaseName}{level}" : pack.Comp.FillBaseName;
            _sprite.LayerSetRsiState((pack.Owner, sprite), fillLayer, state);
            _sprite.LayerSetColor((pack.Owner, sprite), fillLayer, pack.Comp.FillColor);
            _sprite.LayerSetVisible((pack.Owner, sprite), fillLayer, true);
        }
    }
}

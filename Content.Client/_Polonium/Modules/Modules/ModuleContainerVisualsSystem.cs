using Content.Client._Polonium.Modules.ModSuit.Events;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared._Polonium.Modules;
using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared._Polonium.Modules.Modules;
using Robust.Client.GameObjects;
using Robust.Shared.Reflection;

namespace Content.Client._Polonium.Modules.Modules;

public sealed partial class ModuleContainerVisualsSystem : SharedModuleContainerVisualsSystem
{
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private IReflectionManager _reflection = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    internal string LayerPrefix = string.Empty;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModuleContainerVisualsComponent, ModuleRelayedEvent<AppearanceChangeEvent>>((u, c, a) => OnAppearanceChange((u, c), a.ModuleOwner, ref a.Args));
        SubscribeLocalEvent<ModuleContainerVisualsComponent, ModuleRelayedEvent<GetEquipmentVisualsEvent>>((u, c, a) => OnGetVisuals((u, c), ref a.Args));
        SubscribeLocalEvent<ModuleContainerVisualsComponent, ModuleRelayedEvent<GetInhandVisualsEvent>>((u, c, a) => OnItemVisuals((u, c), ref a.Args));

        SubscribeLocalEvent<ModuleContainerVisualsComponent, ModuleRelayedEvent<ModSuitDeployedPartRelayedEvent<AppearanceChangeEvent>>>((u, c, a) => OnDeployedAppearanceChange((u, c), ref a.Args));
        SubscribeLocalEvent<ModuleContainerVisualsComponent, ModuleRelayedEvent<ModSuitDeployedPartRelayedEvent<GetEquipmentVisualsEvent>>>((u, c, a) => OnDeployedGetVisuals((u, c), ref a.Args));
        SubscribeLocalEvent<ModuleContainerVisualsComponent, ModuleRelayedEvent<ModSuitDeployedPartRelayedEvent<GetInhandVisualsEvent>>>((u, c, a) => OnItemVisuals((u, c), ref a.Args.Args));

        SubscribeLocalEvent<ModuleContainerVisualsComponent, ModuleRelayedEvent<ModSuitDeployedPartRelayedEvent<ModSuitSealedGetClothingLayersEvent>>>((u, c, a) => OnSealedGetVisuals((u, c), ref a.Args.Args));
        SubscribeLocalEvent<ModuleContainerVisualsComponent, ModuleRelayedEvent<ModSuitDeployedPartRelayedEvent<ModSuitSealedGetIconLayersEvent>>>((u, c, a) => OnSealedGetIconVisuals((u, c), ref a.Args.Args));

        LayerPrefix = _reflection.GetEnumReference(ModuleContainerVisualLayers.Layer);
    }

    private void OnDeployedAppearanceChange(Entity<ModuleContainerVisualsComponent> ent, ref ModSuitDeployedPartRelayedEvent<AppearanceChangeEvent> args)
    {
        // handled in a separate event
        if (HasComp<ModSuitSealableComponent>(args.Part))
            return;

        OnAppearanceChange(ent, args.Part, ref args.Args);
    }

    private void OnAppearanceChange(Entity<ModuleContainerVisualsComponent> ent, EntityUid spriteOwner, ref AppearanceChangeEvent args)
    {
        if (GetVisualEntity(ent.AsNullable()) is { } visual)
            _item.VisualsChanged(visual);

        if (args.Sprite is not { } sprite)
            return;

        foreach (var layerId in ent.Comp.RevealedIconVisuals)
            _sprite.RemoveLayer((spriteOwner, sprite), layerId);

        ent.Comp.RevealedIconVisuals.Clear();

        // gotta cleanup our prior mess before leaving
        if (!ent.Comp.ItemLayers.TryGetValue(ToggleableModule.IsToggled(ent.Owner), out var layers))
            return;

        foreach (var (_, layer) in UpdateVisuals(layers))
        {
            var layerId = _sprite.AddLayer((spriteOwner, sprite), layer, null);
            ent.Comp.RevealedIconVisuals.Add(layerId);
        }
    }

    private void OnDeployedGetVisuals(Entity<ModuleContainerVisualsComponent> ent, ref ModSuitDeployedPartRelayedEvent<GetEquipmentVisualsEvent> args)
    {
        // handled in a separate event
        if (HasComp<ModSuitSealableComponent>(args.Part))
            return;

        OnGetVisuals(ent, ref args.Args);
    }

    private void OnGetVisuals(Entity<ModuleContainerVisualsComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!ent.Comp.ClothingLayers.TryGetValue(ToggleableModule.IsToggled(ent.Owner), out var layers))
            return;

        if (!layers.TryGetValue(args.Slot, out var layerData))
            return;

        foreach (var (key, layer) in UpdateVisuals(layerData, args.Slot))
            args.Layers.Add((key, layer));
    }

    private void OnItemVisuals(Entity<ModuleContainerVisualsComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (!ent.Comp.InHandLayers.TryGetValue(ToggleableModule.IsToggled(ent.Owner), out var layers))
            return;

        if (!layers.TryGetValue(args.Location, out var layerData))
            return;

        foreach (var (key, layer) in UpdateVisuals(layerData, args.Location))
            args.Layers.Add((key, layer));
    }

    private void OnSealedGetVisuals(Entity<ModuleContainerVisualsComponent> ent, ref ModSuitSealedGetClothingLayersEvent args)
    {
        if (!ent.Comp.ClothingLayers.TryGetValue(ToggleableModule.IsToggled(ent.Owner), out var layers))
            return;

        if (!layers.TryGetValue(args.Slot, out var layerData))
            return;

        foreach (var (_, layer) in UpdateVisuals(layerData, args.Slot))
            args.Layers.Add(layer);
    }

    private void OnSealedGetIconVisuals(Entity<ModuleContainerVisualsComponent> ent, ref ModSuitSealedGetIconLayersEvent args)
    {
        if (!ent.Comp.ItemLayers.TryGetValue(ToggleableModule.IsToggled(ent.Owner), out var layers))
            return;

        foreach (var (_, layer) in UpdateVisuals(layers))
            args.Layers.Add(layer);
    }

    internal IEnumerable<(string, PrototypeLayerData)> UpdateVisuals(List<PrototypeLayerData> layers, object? identifier = null)
    {
        var prefix = identifier == null
            ? $"{LayerPrefix}"
            : $"{identifier}-{LayerPrefix}";

        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            var key = $"{prefix}-{i}";

            yield return (key, layer);
        }
    }
}

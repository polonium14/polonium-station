using Content.Client._Polonium.Modules.ModSuit.Events;
using Content.Shared.Clothing;
using Content.Shared.Item;
using Content.Shared._Polonium.Modules.ModSuit;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Robust.Client.GameObjects;
using System.Linq;

namespace Content.Client._Polonium.Modules.ModSuit;

public partial class ModSuitSystem
{
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string LayerPrefix = "modsuit-sealable-layers";

    private void InitializeSealable()
    {
        SubscribeLocalEvent<ModSuitSealableComponent, AppearanceChangeEvent>(OnSealableAppearanceChange);
        SubscribeLocalEvent<ModSuitSealableComponent, GetEquipmentVisualsEvent>(OnSealableClothingVisuals);
    }

    private void OnSealableAppearanceChange(Entity<ModSuitSealableComponent> ent, ref AppearanceChangeEvent args)
    {
        if (!Appearance.TryGetData<bool>(ent.Owner, ModSuitSealedVisuals.Sealed, out var isSealed))
            return;

        _item.VisualsChanged(ent.Owner);

        if (args.Sprite is not { } sprite)
            return;

        foreach (var layer in ent.Comp.RevealedLayers)
            _sprite.RemoveLayer((ent.Owner, sprite), layer);

        ent.Comp.RevealedLayers.Clear();

        if (!ent.Comp.IconLayers.TryGetValue(isSealed, out var layers))
            return;

        var ev = new ModSuitSealedGetIconLayersEvent();
        RaiseLocalEvent(ent.Owner, ev);

        AddIconLayers(ent, layers, sprite);
        AddIconLayers(ent, ev.Layers, sprite);
    }

    private void AddIconLayers(Entity<ModSuitSealableComponent> ent, List<PrototypeLayerData> layers, SpriteComponent sprite)
    {
        foreach (var layer in layers)
        {
            layer.MapKeys ??= [];
            layer.MapKeys.Add(LayerPrefix);

            ent.Comp.RevealedLayers.Add(_sprite.AddLayer((ent.Owner, sprite), layer, null));
        }
    }

    private void OnSealableClothingVisuals(Entity<ModSuitSealableComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!Appearance.TryGetData<bool>(ent.Owner, ModSuitSealedVisuals.Sealed, out var isSealed))
            return;

        if (!ent.Comp.ClothingLayers.TryGetValue(isSealed, out var clothingLayers))
            return;

        if (!clothingLayers.TryGetValue(args.Slot, out var layers))
            return;

        var ev = new ModSuitSealedGetClothingLayersEvent(args.Slot);
        RaiseLocalEvent(ent.Owner, ev);

        AddClothingLayers(layers, ref args);
        AddClothingLayers(ev.Layers, ref args);
    }

    private static void AddClothingLayers(List<PrototypeLayerData> layers, ref GetEquipmentVisualsEvent args)
    {
        List<(string, PrototypeLayerData)> toAdd = [];

        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];

            var key = layer.MapKeys?.FirstOrDefault();
            if (key == null)
            {
                key = $"{LayerPrefix}-{args.Slot}-{i + args.Layers.Count}";
            }
            else
            {
                key = $"{LayerPrefix}-{args.Slot}-{key}";
                layer.MapKeys = [key];
            }

            toAdd.Add((key, layer));
        }

        args.Layers.AddRange(toAdd);
    }
}

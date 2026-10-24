using Content.Client._Polonium.Modules.ModSuit.Events;
using Content.Shared.Clothing;
using Content.Shared.Item;
using Content.Shared._Polonium.Modules.ModSuit;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Client._Polonium.Modules.ModSuit;

public partial class ModSuitSystem
{
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string LayerPrefix = "modsuit-sealable-layers";
    private const string LightsSuffix = "-lights";
    private const string UnpoweredSuffix = "-unpowered";

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

        foreach (var layer in ent.Comp.RevealedLayers.OrderDescending())
            _sprite.RemoveLayer((ent.Owner, sprite), layer);

        ent.Comp.RevealedLayers.Clear();

        var hidden = IsHidden(ent.Owner);
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
            _sprite.LayerSetVisible((ent.Owner, sprite), i, !hidden);

        if (!TryGetIconLayers(ent, isSealed, out var layers))
            return;

        var ev = new ModSuitSealedGetIconLayersEvent();
        RaiseLocalEvent(ent.Owner, ev);

        if (!hidden)
        {
            var overlays = GetModuleOverlayLayers(ent, layers);

            AddIconLayers(ent, layers, sprite);
            AddIconLayers(ent, GetCoreLightLayers(ent, layers), sprite);
            AddIconLayers(ent, overlays, sprite);
            AddIconLayers(ent, GetCoreLightLayers(ent, overlays), sprite);
        }

        AddIconLayers(ent, ev.Layers, sprite);
    }

    private void AddIconLayers(Entity<ModSuitSealableComponent> ent, List<PrototypeLayerData> layers, SpriteComponent sprite)
    {
        foreach (var layer in layers)
            ent.Comp.RevealedLayers.Add(_sprite.AddLayer((ent.Owner, sprite), layer, null));
    }

    private void OnSealableClothingVisuals(Entity<ModSuitSealableComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!Appearance.TryGetData<bool>(ent.Owner, ModSuitSealedVisuals.Sealed, out var isSealed))
            return;

        if (!TryGetClothingLayers(ent, isSealed, args.Slot, out var layers))
            return;

        var ev = new ModSuitSealedGetClothingLayersEvent(args.Slot);
        RaiseLocalEvent(ent.Owner, ev);

        if (!IsHidden(ent.Owner))
        {
            var overlays = GetModuleOverlayLayers(ent, layers);

            AddClothingLayers(layers, ref args);
            AddClothingLayers(GetCoreLightLayers(ent, layers), ref args);
            AddClothingLayers(overlays, ref args);
            AddClothingLayers(GetCoreLightLayers(ent, overlays), ref args);
        }

        AddClothingLayers(ev.Layers, ref args);
    }

    /// <summary>
    /// Whether an installed module draws itself in place of this part.
    /// </summary>
    private bool IsHidden(EntityUid part)
    {
        return Appearance.TryGetData<bool>(part, ModSuitSealedVisuals.Hidden, out var hidden) && hidden;
    }

    /// <summary>
    /// Layers that tint the suit's lights in the colour of its core, using the "-lights" mask of each layer's state.
    /// </summary>
    private List<PrototypeLayerData> GetCoreLightLayers(Entity<ModSuitSealableComponent> ent, List<PrototypeLayerData> layers)
    {
        if (IsUnpowered(ent.Owner))
            return [];

        var suit = CompOrNull<ModSuitDeployedPartComponent>(ent.Owner)?.Suit ?? ent.Owner;
        if (GetCore(suit)?.LightColor is not { } color)
            return [];

        return GetVariantLayers(layers, LightsSuffix, color);
    }

    /// <summary>
    /// Layers that draw the installed modules on the suit, using the overlay states named after each module.
    /// </summary>
    private List<PrototypeLayerData> GetModuleOverlayLayers(Entity<ModSuitSealableComponent> ent, List<PrototypeLayerData> layers)
    {
        List<PrototypeLayerData> overlays = [];

        if (!Appearance.TryGetData<string>(ent.Owner, ModSuitSealedVisuals.Overlays, out var suffixes) || suffixes.Length == 0)
            return overlays;

        foreach (var suffix in suffixes.Split(','))
            overlays.AddRange(GetVariantLayers(layers, "-" + suffix));

        return overlays;
    }

    /// <summary>
    /// Copies of the layers that use the "state-suffix" variant of their state, for the layers whose RSI has one.
    /// Unpowered states share the variants of their powered state.
    /// </summary>
    private List<PrototypeLayerData> GetVariantLayers(List<PrototypeLayerData> layers, string suffix, Color? color = null)
    {
        List<PrototypeLayerData> variants = [];

        foreach (var layer in layers)
        {
            if (layer.RsiPath == null || layer.State == null)
                continue;

            var baseState = layer.State.EndsWith(UnpoweredSuffix) ? layer.State[..^UnpoweredSuffix.Length] : layer.State;
            var state = baseState + suffix;
            if (!_cache.TryGetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / layer.RsiPath, out var rsi) ||
                !rsi.RSI.TryGetState(state, out _))
                continue;

            variants.Add(new PrototypeLayerData { RsiPath = layer.RsiPath, State = state, Color = color });
        }

        return variants;
    }

    private bool TryGetIconLayers(Entity<ModSuitSealableComponent> ent, bool isSealed, [NotNullWhen(true)] out List<PrototypeLayerData>? layers)
    {
        if (IsUnpowered(ent.Owner) && ent.Comp.UnpoweredIconLayers.TryGetValue(isSealed, out layers))
            return true;

        return ent.Comp.IconLayers.TryGetValue(isSealed, out layers);
    }

    private bool TryGetClothingLayers(Entity<ModSuitSealableComponent> ent, bool isSealed, string slot, [NotNullWhen(true)] out List<PrototypeLayerData>? layers)
    {
        layers = null;

        if (IsUnpowered(ent.Owner) &&
            ent.Comp.UnpoweredClothingLayers.TryGetValue(isSealed, out var unpowered) &&
            unpowered.TryGetValue(slot, out layers))
            return true;

        return ent.Comp.ClothingLayers.TryGetValue(isSealed, out var clothing) && clothing.TryGetValue(slot, out layers);
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

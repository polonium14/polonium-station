using Content.Client.Clothing;
using Content.Client.Items.Systems;
using Content.Shared._Polonium.Item;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client._Polonium.Item;

public sealed partial class UnshadedHeldVisualsSystem : EntitySystem
{
    private const string Suffix = "-unshaded";
    private const string Shader = "unshaded";

    [Dependency] private IResourceCache _resCache = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnshadedHeldVisualsComponent, GetInhandVisualsEvent>(OnGetInhandVisuals, after: [typeof(ItemSystem)]);
        SubscribeLocalEvent<UnshadedHeldVisualsComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals, after: [typeof(ClientClothingSystem)]);
    }

    private void OnGetInhandVisuals(Entity<UnshadedHeldVisualsComponent> ent, ref GetInhandVisualsEvent args)
    {
        AddUnshadedTwins(args.Layers);
    }

    private void OnGetEquipmentVisuals(Entity<UnshadedHeldVisualsComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        AddUnshadedTwins(args.Layers);
    }

    private void AddUnshadedTwins(List<(string, PrototypeLayerData)> layers)
    {
        var count = layers.Count;
        for (var i = 0; i < count; i++)
        {
            var (key, layer) = layers[i];
            if (layer.State is not { } state || layer.RsiPath is not { } path || layer.Shader != null)
                continue;

            var twin = state + Suffix;
            if (!_resCache.TryGetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / path, out var rsi)
                || !rsi.RSI.TryGetState(twin, out _))
                continue;

            layers.Add((key + Suffix, new PrototypeLayerData
            {
                RsiPath = path,
                State = twin,
                Shader = Shader,
                Scale = layer.Scale,
            }));
        }
    }
}

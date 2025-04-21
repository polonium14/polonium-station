using Content.Server.Botany.Components;
using Content.Shared.CartridgeLoader;

namespace Content.Server.CartridgeLoader.Cartridges;

public sealed class PlantScanCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoaderSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantScanCartridgeComponent, CartridgeAddedEvent>(OnCartridgeAdded);
        SubscribeLocalEvent<PlantScanCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);
    }

    private void OnCartridgeAdded(Entity<PlantScanCartridgeComponent> ent, ref CartridgeAddedEvent args)
    {
        var analyzer = EnsureComp<PlantAnalyzerComponent>(args.Loader);
    }

    private void OnCartridgeRemoved(Entity<PlantScanCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        // only remove when the program itself is removed
        if (!_cartridgeLoaderSystem.HasProgram<PlantScanCartridgeComponent>(args.Loader))
        {
            RemComp<PlantAnalyzerComponent>(args.Loader);
        }
    }
}

using Content.Shared.Overlays;
using Robust.Shared.Containers;

namespace Content.Client.NightVision;

public sealed partial class NightVisionSystem
{
    [Dependency] private SharedContainerSystem _container = default!;

    // The R.I.G. night vision module adds and removes the component on a suit that is already worn.
    [SubscribeLocalEvent]
    private void OnWornNightVisionStartup(Entity<NightVisionComponent> ent, ref ComponentStartup args)
    {
        RefreshWearer(ent);
    }

    [SubscribeLocalEvent]
    private void OnWornNightVisionRemove(Entity<NightVisionComponent> ent, ref ComponentRemove args)
    {
        RefreshWearer(ent);
    }

    private void RefreshWearer(Entity<NightVisionComponent> ent)
    {
        if (ent.Comp.RelayOverlay && _container.TryGetContainingContainer((ent.Owner, null, null), out var container))
            RefreshOverlay(container.Owner);
    }
}

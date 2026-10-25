using Content.Server.Atmos.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class BarotraumaSystem
{
    [SubscribeLocalEvent]
    private void OnModSuitSealChanged(Entity<BarotraumaComponent> ent, ref ModSuitWearerSealChangedEvent args)
    {
        UpdateCachedResistances(ent, ent.Comp);
    }
}

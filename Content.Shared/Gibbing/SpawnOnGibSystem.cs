using Content.Shared.Storage;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.Gibbing;

public sealed partial class SpawnOnGibSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SpawnOnGibComponent, BeingGibbedEvent>(OnBeingGibbed);
    }

    private void OnBeingGibbed(Entity<SpawnOnGibComponent> ent, ref BeingGibbedEvent args)
    {
        if (!_net.IsServer)
            return;

        var coords = _transform.GetMoverCoordinates(ent);
        foreach (var proto in EntitySpawnCollection.GetSpawns(ent.Comp.Spawn, _random))
        {
            var spawned = Spawn(proto, coords);
            args.Giblets.Add(spawned);
        }
    }
}

using System.Linq;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
using Content.Shared.Rejuvenate;
using Robust.Shared.Containers;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Leaves a dead organ in the slot of a destroyed one, so it has to be cut out and replaced.
/// </summary>
public sealed partial class DeadOrganSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private OrganRelationSystem _relation = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, OrganDestroyedEvent>(OnOrganDestroyed);
        SubscribeLocalEvent<InitialBodyComponent, RejuvenateEvent>(OnRejuvenate, before: [typeof(BodyRejuvenateSystem)]);
    }

    private void OnOrganDestroyed(Entity<BodyComponent> ent, ref OrganDestroyedEvent args)
    {
        if (!TryComp<OrganIntegrityComponent>(args.Organ, out var integrity)
            || integrity.DeadOrgan is not { } proto
            || !TrySpawnInContainer(proto, ent, BodyComponent.ContainerID, out var dead))
            return;

        // Dismemberment takes organs along with their parent part, so the dead organ needs the same parent.
        if (args.Parent is { } parent)
            _relation.Relate(parent, dead.Value);
    }

    private void OnRejuvenate(Entity<InitialBodyComponent> ent, ref RejuvenateEvent args)
    {
        if (!TryComp<BodyComponent>(ent, out var body) || body.Organs is not { } organs)
            return;

        foreach (var organ in organs.ContainedEntities.ToArray())
        {
            if (!HasComp<DeadOrganComponent>(organ))
                continue;

            // Taken out right away, because deletion is deferred and BodyRejuvenateSystem
            // only respawns organs whose category is missing from the body.
            _container.Remove(organ, organs, force: true);
            QueueDel(organ);
        }
    }
}

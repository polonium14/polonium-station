using System.Numerics;
using Content.Shared._Shitmed.Medical.Surgery;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

public sealed partial class InitialBodySystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InitialBodyComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<InitialBodyComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<ContainerManagerComponent>(ent, out var containerComp))
            return;

        if (TerminatingOrDeleted(ent) || !Exists(ent))
            return;

        if (!_container.TryGetContainer(ent, BodyComponent.ContainerID, out var container, containerComp))
        {
            Log.Error($"Entity {ToPrettyString(ent)} with a {nameof(InitialBodyComponent)} is missing a container ({BodyComponent.ContainerID}).");
            return;
        }

        var xform = Transform(ent);
        var coords = new EntityCoordinates(ent, Vector2.Zero);

        foreach (var proto in ent.Comp.Organs.Values)
        {
            // TODO: When e#6192 is merged replace this all with TrySpawnInContainer...
            var spawn = Spawn(proto, coords);

            if (!_container.Insert(spawn, container, containerXform: xform))
            {
                Log.Error($"Entity {ToPrettyString(ent)} with a {nameof(InitialBodyComponent)} failed to insert an entity: {ToPrettyString(spawn)}.\n");
                Del(spawn);
            }
        }

        EnsureComp<SurgeryTargetComponent>(ent);
    }

    /// <summary>
    /// Whether this body's initial-organ manifest includes the given category - i.e. whether
    /// the species is anatomically expected to have one, regardless of what's currently attached.
    /// </summary>
    public bool ExpectsOrgan(Entity<InitialBodyComponent?> ent, ProtoId<OrganCategoryPrototype> category)
    {
        return Resolve(ent, ref ent.Comp, false) && ent.Comp.Organs.ContainsKey(category);
    }
}

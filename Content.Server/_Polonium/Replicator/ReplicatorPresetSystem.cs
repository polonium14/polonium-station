using Content.Server._Impstation.Replicator;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorPresetSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ReplicatorEvolutionChamberSystem _chamber = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        // The stun ray has to be in hand before the modules boost it.
        SubscribeLocalEvent<ReplicatorPresetComponent, MapInitEvent>(OnMapInit, after: [typeof(ReplicatorSystem)]);
    }

    private void OnMapInit(Entity<ReplicatorPresetComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.JoinNest)
            JoinNearestNest(ent);

        if (!TryComp<ReplicatorModulesComponent>(ent, out var modules))
            return;

        if (ent.Comp.Class is { } classId && _prototype.TryIndex(classId, out var prototype))
            _chamber.Specialize((ent.Owner, modules), prototype);

        foreach (var (moduleId, stages) in ent.Comp.Stages)
        {
            if (!_prototype.TryIndex(moduleId, out var module))
                continue;

            for (var i = 0; i < stages; i++)
            {
                _chamber.Grow((ent.Owner, modules), module);
            }
        }
    }

    private void JoinNearestNest(EntityUid uid)
    {
        if (!TryComp<ReplicatorComponent>(uid, out var replicator) || replicator.MyNest != null)
            return;

        var xform = Transform(uid);
        var position = _transform.GetWorldPosition(xform);
        Entity<ReplicatorNestComponent>? nearest = null;
        var nearestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<ReplicatorNestComponent, TransformComponent>();
        while (query.MoveNext(out var nestUid, out var nest, out var nestXform))
        {
            if (nestXform.MapID != xform.MapID)
                continue;

            var distance = (_transform.GetWorldPosition(nestXform) - position).LengthSquared();
            if (distance >= nearestDistance)
                continue;

            nearest = new Entity<ReplicatorNestComponent>(nestUid, nest);
            nearestDistance = distance;
        }

        if (nearest is not { } found)
            return;

        replicator.MyNest = found.Owner;
        found.Comp.SpawnedMinions.Add(uid);
    }
}

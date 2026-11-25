using Content.Shared.Physics;

namespace Content.Shared._Polonium.Physics;

public sealed partial class PreventCollideTargetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PreventCollideComponent, ComponentShutdown>(OnPreventCollideShutdown);
        SubscribeLocalEvent<PreventCollideTargetComponent, EntityTerminatingEvent>(OnTargetTerminating);
    }

    /// <summary>
    /// Stops the entity from colliding with the target until the target is deleted.
    /// </summary>
    public void PreventCollide(EntityUid uid, EntityUid target)
    {
        if (TerminatingOrDeleted(target))
            return;

        var comp = EnsureComp<PreventCollideComponent>(uid);
        Untrack(uid, comp.Uid);
        comp.Uid = target;
        Dirty(uid, comp);

        EnsureComp<PreventCollideTargetComponent>(target).Colliders.Add(uid);
    }

    private void OnPreventCollideShutdown(Entity<PreventCollideComponent> ent, ref ComponentShutdown args)
    {
        Untrack(ent, ent.Comp.Uid);
    }

    private void OnTargetTerminating(Entity<PreventCollideTargetComponent> ent, ref EntityTerminatingEvent args)
    {
        foreach (var collider in ent.Comp.Colliders)
        {
            if (!TerminatingOrDeleted(collider)
                && TryComp<PreventCollideComponent>(collider, out var comp)
                && comp.Uid == ent.Owner)
                RemComp(collider, comp);
        }
    }

    private void Untrack(EntityUid collider, EntityUid target)
    {
        if (TerminatingOrDeleted(target) || !TryComp<PreventCollideTargetComponent>(target, out var comp))
            return;

        comp.Colliders.Remove(collider);
        if (comp.Colliders.Count == 0)
            RemComp(target, comp);
    }
}

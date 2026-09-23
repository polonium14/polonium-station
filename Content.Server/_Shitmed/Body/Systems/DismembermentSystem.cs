using System.Linq;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Robust.Shared.Containers;

namespace Content.Server._Shitmed.Body.Systems;

public sealed partial class DismembermentSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private OrganRelationSystem _relations = default!;
    [Dependency] private TraumaSystem _trauma = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoundableComponent, WoundableSeverityChangedEvent>(OnSeverityChanged);
    }

    private void OnSeverityChanged(Entity<WoundableComponent> ent, ref WoundableSeverityChangedEvent args)
    {
        if (args.New != WoundableSeverity.Severed)
            return;

        if (!TryComp<OrganComponent>(ent, out var organComp) || organComp.Body is not { } bodyUid)
            return;

        if (!TryComp<BodyComponent>(bodyUid, out var body))
            return;

        Dismember(ent.Owner, bodyUid, body);
    }

    private void Dismember(EntityUid organ, EntityUid bodyUid, BodyComponent body)
    {
        if (body.Organs is null)
            return;

        var category = CompOrNull<OrganComponent>(organ)?.Category?.Id;

        // Body organs share a flat container, but the anatomical tree determines which
        // organs must travel with a removed part (for example, the brain inside a head).
        var children = _relations.AllChildren(organ)
            .Select(child => child.Owner)
            .Where(child => CompOrNull<OrganComponent>(child)?.Body == bodyUid)
            .ToArray();

        if (!_container.Remove(organ, body.Organs, force: true))
            return;

        if (HasComp<ChildOrganComponent>(organ))
            _relations.Orphan(organ);
        if (children.Length > 0)
        {
            EnsureComp<DismemberedPartComponent>(organ);
            var contents = _container.EnsureContainer<Container>(organ, DismemberedPartComponent.ContainerId);
            foreach (var child in children)
                _container.Insert(child, contents, force: true);
        }

        if (category is "LegLeft" or "LegRight" or "FootLeft" or "FootRight" or "ArmLeft" or "ArmRight")
            _trauma.RefreshLimbMovementSpeed(bodyUid);
    }
}

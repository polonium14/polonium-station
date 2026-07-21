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

        var category = organComp.Category;

        Dismember(ent.Owner, bodyUid, body);

        if (category is { } cat)
        {
            foreach (var childCategory in LimbTargetMap.GetCascadeChildren(cat))
            {
                if (LimbTargetMap.TryGetOrganByCategory(EntityManager, body, childCategory, out var child))
                    Dismember(child, bodyUid, body);
            }
        }
    }

    private void Dismember(EntityUid organ, EntityUid bodyUid, BodyComponent body)
    {
        if (body.Organs is null)
            return;

        var category = CompOrNull<OrganComponent>(organ)?.Category?.Id;

        if (!_container.Remove(organ, body.Organs, force: true))
            return;

        if (category is "LegLeft" or "LegRight" or "FootLeft" or "FootRight" or "ArmLeft" or "ArmRight")
            _trauma.RefreshLimbMovementSpeed(bodyUid);
    }
}

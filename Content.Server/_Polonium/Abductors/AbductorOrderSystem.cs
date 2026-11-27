using Content.Shared._Polonium.Abductors;
using Content.Shared._Shitmed.Body;
using Content.Shared.Body;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorOrderSystem : EntitySystem
{
    [Dependency] private AbductorShipSystem _ship = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private readonly List<(AbductorOrderPrototype Order, EntityUid Organ)> _candidates = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AbductorSpecimenComponent, OrganRemovedFromEvent>(OnOrganRemoved);
    }

    /// <summary>
    /// Requests an extra procedure on a specimen that has just come aboard.
    /// </summary>
    public void Issue(Entity<AbductorShipComponent> ship, EntityUid specimen)
    {
        if (HasComp<AbductorOrderComponent>(specimen)
            || _ship.IsCatalogued(ship, specimen)
            || !TryComp<BodyComponent>(specimen, out var body))
        {
            return;
        }

        var donorAboard = HasDonorAboard(ship, specimen);
        var totalWeight = 0f;
        _candidates.Clear();
        foreach (var order in _prototype.EnumeratePrototypes<AbductorOrderPrototype>())
        {
            if (order.FromDonor && !donorAboard
                || !LimbTargetMap.TryGetOrganByCategory(EntityManager, body, order.Category, out var organ))
            {
                continue;
            }

            _candidates.Add((order, organ));
            totalWeight += order.Weight;
        }

        if (_candidates.Count == 0)
            return;

        var roll = _random.NextFloat(totalWeight);
        var picked = _candidates[^1];
        foreach (var candidate in _candidates)
        {
            roll -= candidate.Order.Weight;
            if (roll > 0f)
                continue;

            picked = candidate;
            break;
        }

        var component = EnsureComp<AbductorOrderComponent>(specimen);
        component.Order = picked.Order.ID;
        component.Original = picked.Organ;
        _ship.Say(ship, picked.Order.Line);
    }

    /// <summary>
    /// Closes the specimen's order. Null means it never had one.
    /// </summary>
    public bool? Settle(EntityUid specimen)
    {
        if (!TryComp<AbductorOrderComponent>(specimen, out var order))
            return null;

        var done = IsFulfilled(specimen, order);
        RemComp<AbductorOrderComponent>(specimen);
        return done;
    }

    private bool IsFulfilled(EntityUid specimen, AbductorOrderComponent order)
    {
        if (!_prototype.TryIndex(order.Order, out var proto)
            || !TryComp<BodyComponent>(specimen, out var body)
            || !LimbTargetMap.TryGetOrganByCategory(EntityManager, body, proto.Category, out var organ)
            || organ == order.Original)
        {
            return false;
        }

        return !proto.FromDonor
            || TryComp<AbductorHarvestedOrganComponent>(organ, out var harvested) && harvested.Donor != specimen;
    }

    private bool HasDonorAboard(Entity<AbductorShipComponent> ship, EntityUid specimen)
    {
        var query = EntityQueryEnumerator<AbductorSpecimenComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (uid != specimen
                && xform.GridUid == ship.Owner
                && !_ship.IsCatalogued(ship, uid)
                && !_mobState.IsDead(uid))
            {
                return true;
            }
        }

        return false;
    }

    private void OnOrganRemoved(Entity<AbductorSpecimenComponent> ent, ref OrganRemovedFromEvent args)
    {
        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(args.Organ))
            return;

        EnsureComp<AbductorHarvestedOrganComponent>(args.Organ).Donor = ent;
    }
}

using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Body;

/// <summary>
/// An organ taken out of a body slowly dies, unless it lies in a freezer, a morgue or another container that stops rot.
/// The brain is left alone, because it can hold a player, and so are organs inside a severed head or limb.
/// </summary>
public sealed partial class OrganDecaySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private OrganHealingSystem _healing = default!;

    private static readonly ProtoId<OrganCategoryPrototype> Brain = "Brain";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private TimeSpan _nextDecay;
    private readonly List<Entity<OrganIntegrityComponent>> _decaying = [];

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextDecay)
            return;

        _nextDecay = _timing.CurTime + Interval;

        var lifetime = _cfg.GetCVar(CCVars.OrgansDecayTime);
        if (lifetime <= 0f)
            return;

        // ponytail: walks every organ in the game every 10 seconds; track loose organs with a marker if it shows up in profiles.
        // Collected first, because a dying organ is deleted and replaced.
        _decaying.Clear();
        var query = EntityQueryEnumerator<OrganIntegrityComponent, OrganComponent>();
        while (query.MoveNext(out var uid, out var integrity, out var organ))
        {
            // An organ still inside a severed head or limb keeps its parent part and stays fresh,
            // so a reattached head does not come back with dead eyes.
            if (organ.Body == null
                && organ.Category != Brain
                && integrity.OrganIntegrity > FixedPoint2.Zero
                && CompOrNull<ChildOrganComponent>(uid)?.Parent == null
                && !IsPreserved(uid))
                _decaying.Add((uid, integrity));
        }

        var share = (float) Interval.TotalSeconds / lifetime;
        foreach (var organ in _decaying)
        {
            _healing.DamageOrgan(organ, organ.Comp.IntegrityCap * share);
            if (organ.Comp.OrganIntegrity > FixedPoint2.Zero || organ.Comp.DeadOrgan is not { } dead)
                continue;

            // Inside a body the trauma system swaps a destroyed organ for a dead one; a loose organ has nobody to do it.
            SpawnNextToOrDrop(dead, organ);
            QueueDel(organ);
        }
    }

    private bool IsPreserved(EntityUid uid)
    {
        while (_container.TryGetContainingContainer(uid, out var container))
        {
            if (HasComp<AntiRottingContainerComponent>(container.Owner))
                return true;

            uid = container.Owner;
        }

        return false;
    }
}

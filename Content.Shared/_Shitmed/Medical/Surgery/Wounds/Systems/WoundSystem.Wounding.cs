using System.Linq;
using System.Numerics;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;

public sealed partial class WoundSystem
{
    private void InitWounding()
    {
        SubscribeLocalEvent<WoundableComponent, ComponentInit>(OnWoundableInit);
        SubscribeLocalEvent<WoundableComponent, MapInitEvent>(OnWoundableMapInit);
        SubscribeLocalEvent<WoundableComponent, DamageDealtEvent>(OnDamageDealt);

        SubscribeLocalEvent<WoundComponent, EntGotInsertedIntoContainerMessage>(OnWoundInserted);
        SubscribeLocalEvent<WoundComponent, EntGotRemovedFromContainerMessage>(OnWoundRemoved);
    }

    private void OnWoundableInit(Entity<WoundableComponent> ent, ref ComponentInit args)
    {
        ent.Comp.SortedThresholds = ent.Comp.Thresholds
            .OrderByDescending(kv => kv.Value)
            .ToArray();

        if (ent.Comp.WoundableIntegrity <= FixedPoint2.Zero)
            ent.Comp.WoundableIntegrity = ent.Comp.IntegrityCap;
    }

    private void OnWoundableMapInit(Entity<WoundableComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Wounds = _container.EnsureContainer<Container>(ent, WoundableComponent.WoundContainerId);
        ent.Comp.Bone = _container.EnsureContainer<Container>(ent, WoundableComponent.BoneContainerId);

        if (HasComp<BonelessComponent>(ent) || ent.Comp.Bone is not { } boneContainer)
            return;

        var bone = Spawn(ent.Comp.BoneEntity, new EntityCoordinates(ent, Vector2.Zero));

        if (!TryComp<BoneComponent>(bone, out var boneComp) || !_container.Insert(bone, boneContainer))
        {
            Del(bone);
            return;
        }

        boneComp.BoneWoundable = ent.Owner;
        Dirty(ent, ent.Comp);
    }

    private void OnWoundInserted(Entity<WoundComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        // Authoritative recompute is server-only (see OnDamageDealt for why) — the client
        // still needs HoldingWoundable set locally since it's read before the networked
        // value necessarily arrives (e.g. immediately after a predicted-looking spawn).
        ent.Comp.HoldingWoundable = args.Container.Owner;

        if (!_net.IsServer || !TryComp<WoundableComponent>(args.Container.Owner, out var woundable))
            return;

        UpdateWoundableIntegrity(args.Container.Owner, woundable);
        CheckWoundableSeverityThresholds(args.Container.Owner, woundable);
    }

    private void OnWoundRemoved(Entity<WoundComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (!_net.IsServer || !TryComp<WoundableComponent>(args.Container.Owner, out var woundable))
            return;

        UpdateWoundableIntegrity(args.Container.Owner, woundable);
        CheckWoundableSeverityThresholds(args.Container.Owner, woundable);
    }

    /// <summary>
    /// Core damage-intake hook: reacts to the limb-organ's own DamageDealtEvent (populated
    /// by BodyDamageBridgeSystem mirroring targeted mob damage onto this organ) and turns
    /// net-positive deltas into wounds, net-negative deltas into healing.
    ///
    /// Server-only: wound induction isn't predicted (unlike movement/combat-hit feedback,
    /// there's no responsiveness need for the client to guess at wound state), and running
    /// this shared-code pathway on the client too caused a real bug — the client's own
    /// local recompute of WoundableIntegrity could run before a newly-replicated wound
    /// entity's own networked severity had arrived, computing a stale total that then
    /// permanently stomped the correctly-replicated server value (nothing re-triggers a
    /// client-side recompute afterward). Simplest fix: don't compute it client-side at all
    /// — WoundableIntegrity/WoundableSeverity are AutoNetworkedFields, so the client just
    /// receives the authoritative value directly.
    /// </summary>
    private void OnDamageDealt(Entity<WoundableComponent> ent, ref DamageDealtEvent args)
    {
        if (!_net.IsServer || !ent.Comp.AllowWounds || !_timing.IsFirstTimePredicted || _suppressWoundInduction)
            return;

        foreach (var (damageType, damageValue) in args.Damage.DamageDict)
        {
            if (damageValue == FixedPoint2.Zero)
                continue;

            if (damageValue < 0)
            {
                HealWoundsCore(ent, -damageValue, damageType, out _, ent.Comp);
                continue;
            }

            var woundTarget = ent;

            if (ent.Comp.WoundableIntegrity <= FixedPoint2.Zero
                && TryComp<OrganComponent>(ent, out var organ)
                && organ.Body is { } bodyUid
                && GetDamageRedirectTarget(bodyUid, ent, damageType) is { } redirect
                && redirect != ent.Owner
                && TryComp<WoundableComponent>(redirect, out var redirectComp))
            {
                woundTarget = (redirect, redirectComp);
            }

            TryInduceWound(woundTarget, damageType, damageValue, out _, woundTarget.Comp);
        }

        ent.Comp.LastDamageTime = _timing.CurTime;

        UpdateWoundableIntegrity(ent, ent.Comp);
        CheckWoundableSeverityThresholds(ent, ent.Comp);
    }
}

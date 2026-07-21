using System.Linq;
using System.Numerics;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared._Shitmed.Body;

public sealed partial class BodyRejuvenateSystem : EntitySystem
{
    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(EntityUid uid, BodyComponent component, RejuvenateEvent args)
    {
        if (component.Organs is null)
            return;

        if (_trauma.TryGetBodyTraumas(uid, out var traumas, bodyComp: component))
            foreach (var trauma in traumas)
                _trauma.RemoveTrauma(trauma);

        // Snapshotted: healing a bone back to full can ripple into other container mutations
        // (e.g. TraumaSystem.OnBoneIntegrityChanged deleting held items on a fully-healed hand),
        // so iterating the live Organs container here isn't safe.
        foreach (var organ in component.Organs.ContainedEntities.ToList())
        {
            if (HasComp<DamageableComponent>(organ))
            {
#pragma warning disable CS0618
                var currentDamage = _damageable.GetAllDamage(organ);
#pragma warning restore CS0618
                if (!currentDamage.Empty)
                    _damageable.TryChangeDamage(organ, -currentDamage, ignoreResistances: true, interruptsDoAfters: false, origin: null);
            }

            if (!TryComp<WoundableComponent>(organ, out var woundable))
                continue;

            if (woundable.Bone?.ContainedEntities.FirstOrNull() is { } bone
                && TryComp<BoneComponent>(bone, out var boneComp))
                _trauma.SetBoneIntegrity(bone, boneComp.IntegrityCap, boneComp);

            _wound.TryHaltAllBleeding(organ, woundable);
            _wound.ForceHealWoundsOnWoundable(organ, out _, woundable);
        }

        RespawnMissingOrgans(uid, component);
    }

    /// <summary>
    /// A destroyed vital organ (OrganSeverity.Destroyed) gets QueueDel'd and removed from the
    /// body entirely (see TraumaSystem.Organs.cs's OnOrganSeverityChanged) - healing existing
    /// organs above does nothing for one that's already gone. InitialBodyComponent (present on
    /// every mob that was ever given its starting organs - see InitialBodySystem) is the same
    /// category->EntProtoId manifest character creation spawns from, so it doubles as "what this
    /// body is supposed to have" here: anything in it whose category isn't currently present gets
    /// spawned fresh and inserted. Deliberately unconditional (no search for a detached-but-alive
    /// organ elsewhere to reattach instead) - Rejuvenate is already a full-reset admin tool, not
    /// something used mid-surgery.
    /// </summary>
    private void RespawnMissingOrgans(EntityUid uid, BodyComponent component)
    {
        if (component.Organs is null || !TryComp<InitialBodyComponent>(uid, out var initialBody))
            return;

        var present = new HashSet<string>();
        foreach (var organ in component.Organs.ContainedEntities)
        {
            if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category is { } category)
                present.Add(category);
        }

        var coords = new EntityCoordinates(uid, Vector2.Zero);
        var xform = Transform(uid);

        foreach (var (category, proto) in initialBody.Organs)
        {
            if (present.Contains(category))
                continue;

            var spawn = Spawn(proto, coords);
            if (!_container.Insert(spawn, component.Organs, containerXform: xform))
                Del(spawn);
        }
    }
}

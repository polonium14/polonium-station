using System.Linq;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Body;

public sealed partial class BodyDebugSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private WoundSystem _wound = default!;

    /// <summary>
    /// Most damage a single wound from the window can deal.
    /// </summary>
    public const int MaxWoundDamage = 500;

    /// <summary>
    /// Damage type the limb integrity slider wounds with.
    /// </summary>
    private static readonly ProtoId<DamageTypePrototype> SliderDamage = "Blunt";

    /// <summary>
    /// Sets a body part's integrity, up to its cap, with real damage going down and wound healing going up.
    /// The result can miss the target, because wounds scale and heal by the part's own rules.
    /// </summary>
    public bool TrySetLimbIntegrity(EntityUid body, EntityUid limb, FixedPoint2 value)
    {
        if (!TryGetLimb(body, limb, out var woundable))
            return false;

        var target = FixedPoint2.Clamp(value, FixedPoint2.Zero, woundable.IntegrityCap);
        var current = woundable.WoundableIntegrity;
        if (target < current)
            return TryAddWound(body, limb, SliderDamage, current - target);

        return target == current || _wound.TryHealWoundsAndDamage(limb, target - current, out _, woundable);
    }

    /// <summary>
    /// Damages a body part the way a hit would, which wounds it.
    /// </summary>
    public bool TryAddWound(EntityUid body, EntityUid limb, ProtoId<DamageTypePrototype> type, FixedPoint2 amount)
    {
        return amount > FixedPoint2.Zero
               && TryGetLimb(body, limb, out _)
               && _proto.TryIndex(type, out var damageType)
               && _damageable.TryChangeDamage(limb, new DamageSpecifier(damageType, amount), ignoreResistances: true, interruptsDoAfters: false);
    }

    /// <summary>
    /// Sets the integrity of a body part's bone. Going down fractures it through a real trauma, a full bone has none left.
    /// </summary>
    public bool TrySetBoneIntegrity(EntityUid body, EntityUid limb, FixedPoint2 value)
    {
        if (!TryGetLimb(body, limb, out var woundable)
            || woundable.Bone?.ContainedEntities.FirstOrNull() is not { } bone
            || !TryComp<BoneComponent>(bone, out var boneComp))
            return false;

        var target = FixedPoint2.Clamp(value, FixedPoint2.Zero, boneComp.IntegrityCap);
        var current = boneComp.BoneIntegrity;
        if (target == current)
            return true;

        if (target > current)
        {
            _trauma.SetBoneIntegrity(bone, target, boneComp);
            if (target == boneComp.IntegrityCap && _trauma.TryGetWoundableTrauma(limb, out var traumas, TraumaType.BoneDamage, woundable))
            {
                foreach (var trauma in traumas)
                {
                    _trauma.RemoveTrauma(trauma);
                }
            }

            return true;
        }

        return _intake.DamageBone((limb, woundable), current - target);
    }

    /// <summary>
    /// Heals every wound and all damage of a body part and stops its bleeding.
    /// </summary>
    public bool TryHealLimb(EntityUid body, EntityUid limb)
    {
        if (!TryGetLimb(body, limb, out var woundable))
            return false;

        if (HasComp<DamageableComponent>(limb))
        {
#pragma warning disable CS0618 // The same call BodyRejuvenateSystem heals body parts with.
            var damage = _damageable.GetAllDamage(limb);
#pragma warning restore CS0618
            if (!damage.Empty)
                _damageable.TryChangeDamage(limb, -damage, ignoreResistances: true, interruptsDoAfters: false);
        }

        _wound.TryHaltAllBleeding(limb, woundable, force: true);
        _wound.ForceHealWoundsOnWoundable(limb, out _, woundable);
        return true;
    }

    public bool TryStopBleeding(EntityUid body, EntityUid limb)
    {
        return TryGetLimb(body, limb, out var woundable) && _wound.TryHaltAllBleeding(limb, woundable, force: true);
    }

    /// <summary>
    /// Cuts a body part off. The torso is refused, everything else hangs off it.
    /// </summary>
    public bool TryAmputate(EntityUid body, EntityUid limb)
    {
        if (!TryGetLimb(body, limb, out var woundable) || !woundable.CanRemove || _wound.IsWoundableRoot(limb))
            return false;

        _wound.AmputateWoundableSafely(limb, woundable);
        return !TryGetOrgan(body, limb, out _);
    }

    /// <summary>
    /// Grows back a missing body part from the body's manifest, together with the parts and organs that belong in it.
    /// </summary>
    public bool TryRestoreLimb(EntityUid body, ProtoId<OrganCategoryPrototype> category)
    {
        if (!IsLimbCategory(body, category)
            || !TryComp<BodyComponent>(body, out var bodyComp)
            || !LimbTargetMap.TryGetParentCategory(category, out var parentCategory)
            || !LimbTargetMap.TryGetOrganByCategory(EntityManager, bodyComp, parentCategory, out var parent)
            || !TryInsertOrgan(body, category, parent))
            return false;

        if (_trauma.TryGetWoundableTrauma(parent, out var traumas, TraumaType.Dismemberment))
        {
            foreach (var trauma in traumas)
            {
                _trauma.RemoveTrauma(trauma);
            }
        }

        LimbTargetMap.TryGetOrganByCategory(EntityManager, bodyComp, category, out var limb);
        foreach (var child in LimbTargetMap.GetCascadeChildren(category))
        {
            TryRestoreLimb(body, child);
        }

        foreach (var organ in LimbTargetMap.GetVitalOrganCategories(category))
        {
            TryInsertOrgan(body, organ, limb);
        }

        return true;
    }

    /// <summary>
    /// Puts a missing organ or body part back, whichever the category is.
    /// </summary>
    public bool TryPutBack(EntityUid body, ProtoId<OrganCategoryPrototype> category)
    {
        return IsLimbCategory(body, category) ? TryRestoreLimb(body, category) : TryInsertOrgan(body, category);
    }

    /// <summary>
    /// Lists the body's parts: the ones attached and the ones its manifest says are missing.
    /// </summary>
    public List<BodyDebugLimb> GetLimbs(EntityUid body)
    {
        var rows = new List<BodyDebugLimb>();
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Organs is not { } organs)
            return rows;

        var present = new HashSet<ProtoId<OrganCategoryPrototype>>();
        foreach (var limb in organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(limb, out var organComp) || organComp.Category is not { } category)
                continue;

            present.Add(category);

            // Hands and feet have no wounds, there is nothing to show or change while they are attached.
            if (!TryComp<WoundableComponent>(limb, out var woundable))
                continue;

            TryComp<BoneComponent>(woundable.Bone?.ContainedEntities.FirstOrNull(), out var bone);
            rows.Add(WithStates(new BodyDebugLimb(GetNetEntity(limb), category, Name(limb),
                woundable.WoundableIntegrity,
                woundable.IntegrityCap,
                woundable.WoundableSeverity,
                woundable.Bleeds,
                bone?.BoneIntegrity ?? FixedPoint2.Zero,
                bone?.IntegrityCap ?? FixedPoint2.Zero,
                bone?.BoneSeverity ?? BoneSeverity.Normal,
                _wound.GetWoundableWounds(limb, woundable).Select(wound => (Name(wound), wound.Comp.WoundSeverityPoint)).ToList(),
                woundable.CanRemove && !_wound.IsWoundableRoot(limb)), limb));
        }

        if (!TryComp<InitialBodyComponent>(body, out var manifest))
            return rows;

        foreach (var (category, proto) in manifest.Organs)
        {
            if (present.Contains(category) || !IsLimbPrototype(proto))
                continue;

            rows.Add(new BodyDebugLimb(null, category, _proto.Index(proto).Name,
                FixedPoint2.Zero, FixedPoint2.Zero, WoundableSeverity.Severed, FixedPoint2.Zero,
                FixedPoint2.Zero, FixedPoint2.Zero, BoneSeverity.Normal, new(), false));
        }

        return rows;
    }

    /// <summary>
    /// Damage types that leave a wound, for the window's wound picker.
    /// </summary>
    public List<string> GetWoundTypes()
    {
        return _proto.EnumeratePrototypes<DamageTypePrototype>()
            .Select(type => type.ID)
            .Where(id => _proto.HasIndex<EntityPrototype>($"Wound{id}"))
            .Order()
            .ToList();
    }

    private bool IsLimbCategory(EntityUid body, ProtoId<OrganCategoryPrototype> category)
    {
        return TryGetFreshOrgan(body, category, out var proto) && IsLimbPrototype(proto);
    }

    private bool IsLimbPrototype(EntProtoId proto)
    {
        // Hands and feet are body parts without wounds of their own, so "not an internal organ" is the test.
        return _proto.TryIndex(proto, out var entity) && !entity.HasComp<InternalChildOrganComponent>(Factory);
    }

    private bool TryGetLimb(EntityUid body, EntityUid limb, out WoundableComponent woundable)
    {
        woundable = default!;
        return TryGetOrgan(body, limb, out _) && TryComp(limb, out woundable!);
    }
}

using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

// The Shitmed system's namespace, so this stays one partial class with it.
namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;

/// <summary>
/// Polonium rules for severing limbs: the limb itself has to be badly hurt, a head only comes off
/// a body that is down for good, and explosions sever through the same path as a blade.
/// Also how much of a hit an organ takes.
/// </summary>
public partial class TraumaSystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<OrganCategoryPrototype> HeadCategory = "Head";
    private static readonly ProtoId<DamageTypePrototype> SeverDamageType = "Slash";

    /// <summary>
    /// Whether a cut may sever this limb at all: it is already badly hurt or its bone is broken,
    /// and a head belongs to somebody in critical condition or dead.
    /// </summary>
    private bool CanBeSevered(Entity<WoundableComponent> limb, EntityUid body, ProtoId<OrganCategoryPrototype> category)
    {
        if (category == HeadCategory && !_mobState.IsIncapacitated(body))
            return false;

        if (limb.Comp.WoundableSeverity >= WoundableSeverity.Severe)
            return true;

        return limb.Comp.Bone?.ContainedEntities.FirstOrNull() is { } bone
            && TryComp<BoneComponent>(bone, out var boneComp)
            && boneComp.BoneSeverity == BoneSeverity.Broken;
    }

    /// <summary>
    /// Integrity an organ loses to a hit of this severity. The organs are small next to the hits that reach them,
    /// so they take only a share.
    /// </summary>
    public FixedPoint2 OrganTraumaDamage(FixedPoint2 severity)
    {
        return severity * _cfg.GetCVar(CCVars.OrgansTraumaDamageMultiplier);
    }

    private float SeverChance(FixedPoint2 chance)
    {
        return Math.Clamp(chance.Float() * _cfg.GetCVar(CCVars.DismembermentChanceMultiplier), 0f, 1f);
    }

    /// <summary>
    /// Severs a limb the way a blade does, leaving the stump trauma on the part it was attached to.
    /// </summary>
    public void SeverLimb(Entity<WoundableComponent> limb, FixedPoint2 severity)
    {
        // The dismemberment trauma hangs on a wound it makes on the parent part, so it needs no inflicter.
        ApplyTraumas(limb, default, new List<TraumaType> { TraumaType.Dismemberment }, severity);
    }

    /// <summary>
    /// Severs a limb of a body that has no nerve system, so nothing feels it: the stump trauma and the pain
    /// are skipped, the limb just comes off.
    /// </summary>
    private void SeverWithoutNerves(Entity<WoundableComponent> limb)
    {
        if (!_wound.IsWoundableRoot(limb.Owner))
            _wound.AmputateWoundableSafely(limb.Owner, limb.Comp);
    }

    /// <summary>
    /// A limb torn off in a fight takes its own damage away with it, so the part it hung on takes a share of
    /// that damage and the stump bleeds. Surgical amputation does not come through here.
    /// </summary>
    private void HurtBySevering(EntityUid body, Entity<WoundableComponent> limb, EntityUid parent)
    {
        var share = _damageable.GetTotalDamage(limb.Owner).Float() * _cfg.GetCVar(CCVars.DismembermentParentDamageShare);
        var amount = Math.Clamp(share,
            _cfg.GetCVar(CCVars.DismembermentParentDamageMin),
            _cfg.GetCVar(CCVars.DismembermentParentDamageMax));

        // Slash, because a slash wound on the torso rolls no bone or organ trauma of its own.
        if (amount > 0f)
        {
            _damageable.TryChangeDamage(parent,
                new DamageSpecifier(_prototypes.Index(SeverDamageType), amount),
                ignoreResistances: true,
                interruptsDoAfters: false);
        }

        _bloodstream.TryModifyBleedAmount(body, _cfg.GetCVar(CCVars.DismembermentBleed));
    }
}

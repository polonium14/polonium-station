using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

// The Shitmed system's namespace, so this stays one partial class with it.
namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;

/// <summary>
/// Polonium rules for severing limbs: the limb itself has to be badly hurt, a head only comes off
/// a body that is down for good, and explosions sever through the same path as a blade.
/// </summary>
public partial class TraumaSystem
{
    private static readonly ProtoId<OrganCategoryPrototype> HeadCategory = "Head";

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
}

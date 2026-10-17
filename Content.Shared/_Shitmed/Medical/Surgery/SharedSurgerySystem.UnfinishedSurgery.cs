using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;

namespace Content.Shared._Shitmed.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    /// <summary>
    /// Restores an organ's surgery state when the body is rejuvenated, including closing
    /// incisions and affixing reattached parts. Keep this in sync with the markers below.
    /// </summary>
    public void ResetSurgery(EntityUid organ)
    {
        RemComp<IncisionOpenComponent>(organ);
        RemComp<SkinRetractedComponent>(organ);
        RemComp<BleedersClampedComponent>(organ);
        RemComp<InternalBleedersClampedComponent>(organ);
        RemComp<BonesOpenComponent>(organ);
        RemComp<BonesSawedComponent>(organ);
        RemComp<BodyPartSawedComponent>(organ);
        RemComp<SeveredSkinRemovedComponent>(organ);
        RemComp<BoneLeftoversRemovedComponent>(organ);
        RemComp<BodyPartReattachedComponent>(organ);
        RemComp<OrganReattachedComponent>(organ);
        RemComp<LobotomizedComponent>(organ);
        RemComp<PartsRemovedComponent>(organ);
    }

    /// <summary>
    /// True if this organ still carries a marker component left behind by an interrupted
    /// surgery step - an incision that was never closed, retracted skin never stitched back,
    /// clamped bleeders never released, bones opened/sawed but never sealed, or a limb/organ
    /// that was reattached but never affixed. Excludes LobotomizedComponent, which is an
    /// intentional permanent trauma end-state rather than an interrupted surgery step.
    /// </summary>
    public bool HasUnfinishedSurgerySteps(EntityUid organ)
    {
        return HasComp<IncisionOpenComponent>(organ)
            || HasComp<SkinRetractedComponent>(organ)
            || HasComp<BleedersClampedComponent>(organ)
            || HasComp<InternalBleedersClampedComponent>(organ)
            || HasComp<BonesOpenComponent>(organ)
            || HasComp<BonesSawedComponent>(organ)
            || HasComp<BodyPartSawedComponent>(organ)
            || HasComp<SeveredSkinRemovedComponent>(organ)
            || HasComp<BoneLeftoversRemovedComponent>(organ)
            || HasComp<BodyPartReattachedComponent>(organ)
            || HasComp<OrganReattachedComponent>(organ);
    }
}

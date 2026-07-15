using System.Linq;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.EntityEffects.Effects;

/// <summary>
/// Gradually heals every bone on the target's body, restoring each bone's integrity back up
/// to its cap (undoing Damaged/Cracked/Broken severity alike). This is called once per
/// metabolism tick for as long as the reagent is in the bloodstream, and heals each bone by
/// a fraction of its cap equal to (this tick's share of <see cref="HealBones.FullHealDose"/>)
/// - so a dose of exactly FullHealDose finishes healing on the same tick it finishes
/// metabolizing, a smaller dose heals proportionally less, and a bigger dose just finishes
/// early (and clamps, it's a no-op once a bone is already at full integrity).
/// <see cref="HealBones.MetabolismRate"/> must match the reagent's own "rate:" in its
/// metabolism entry (or the 0.5 engine default if unset) for the timing to line up.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class HealBonesEntityEffectSystem : EntityEffectSystem<BodyComponent, HealBones>
{
    [Dependency] private TraumaSystem _trauma = default!;

    protected override void Effect(Entity<BodyComponent> entity, ref EntityEffectEvent<HealBones> args)
    {
        if (entity.Comp.Organs is null)
            return;

        // How much of the reagent is removed on a single metabolism tick, as a fraction of
        // the dose that's meant to fully heal every bone - that's how much of the cap each
        // bone heals this tick.
        var healFraction = (args.Effect.MetabolismRate * args.Scale / args.Effect.FullHealDose).Float();

        // Snapshotted: healing a bone back to full can ripple into other container mutations
        // (TraumaSystem.OnBoneIntegrityChanged deleting held items on a fully-healed hand),
        // so iterating the live Organs container mid-loop isn't safe - see BodyRejuvenateSystem.
        foreach (var organ in entity.Comp.Organs.ContainedEntities.ToList())
        {
            if (!TryComp<WoundableComponent>(organ, out var woundable) || woundable.Bone is null)
                continue;

            foreach (var bone in woundable.Bone.ContainedEntities.ToList())
            {
                if (!TryComp<BoneComponent>(bone, out var boneComp))
                    continue;

                if (boneComp.BoneIntegrity >= boneComp.IntegrityCap)
                    continue;

                var newIntegrity = FixedPoint2.Min(boneComp.IntegrityCap, boneComp.BoneIntegrity + boneComp.IntegrityCap * healFraction);
                _trauma.SetBoneIntegrity(bone, newIntegrity, boneComp);
            }
        }
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class HealBones : EntityEffectBase<HealBones>
{
    /// <summary>
    /// The dose that, taken all at once, finishes healing every bone right as it finishes
    /// metabolizing.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 FullHealDose;

    /// <summary>
    /// The reagent's own per-tick metabolism rate (its metabolism entry's "rate:", or 0.5 if
    /// that's left unset) - used to pace the heal against it.
    /// </summary>
    [DataField]
    public FixedPoint2 MetabolismRate = FixedPoint2.New(0.5f);

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-heal-bones", ("chance", Probability));
}

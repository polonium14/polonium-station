using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Random;

namespace Content.Shared._Polonium.Body;

/// <summary>
/// How likely a hit on the part that holds this organ lands on it, against the other organs there.
/// Organs without it weigh 1.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganTraumaWeightComponent : Component
{
    /// <summary>
    /// Relative weight, roughly the organ's size: big organs are hit more often.
    /// </summary>
    [DataField(required: true)]
    public float Weight = 1f;
}

/// <summary>
/// Organ damage from hits on the torso and head: a battered part exposes its organs more,
/// an intact ribcage or skull shields them.
/// </summary>
public sealed partial class OrganTraumaSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>
    /// Added organ damage chance at a fully damaged part, scaled down linearly with the part's damage.
    /// </summary>
    public const float PartDamageChance = 0.3f;

    /// <summary>
    /// Fraction of organ hits an intact bone stops.
    /// </summary>
    public const float BoneShield = 0.4f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoundableComponent, TraumaChanceDeductionEvent>(OnChanceDeduction);
        SubscribeLocalEvent<WoundableComponent, BeforeTraumaInducedEvent>(OnBeforeTrauma);
    }

    private void OnChanceDeduction(Entity<WoundableComponent> ent, ref TraumaChanceDeductionEvent args)
    {
        if (args.TraumaType != TraumaType.OrganDamage || ent.Comp.IntegrityCap <= 0)
            return;

        var damaged = (ent.Comp.IntegrityCap - ent.Comp.WoundableIntegrity) / ent.Comp.IntegrityCap;
        args.ChanceDeduction -= damaged * PartDamageChance;
    }

    private void OnBeforeTrauma(Entity<WoundableComponent> ent, ref BeforeTraumaInducedEvent args)
    {
        if (args.Cancelled || args.TraumaType != TraumaType.OrganDamage)
            return;

        foreach (var bone in ent.Comp.Bone?.ContainedEntities ?? [])
        {
            // Only the trauma system rolls this, on the server, so the roll is never predicted.
            if (TryComp<BoneComponent>(bone, out var boneComp)
                && boneComp.BoneSeverity < BoneSeverity.Cracked
                && _random.Prob(BoneShield))
                args.Cancelled = true;

            return;
        }
    }

    /// <summary>
    /// Picks the organ a hit lands on, weighted by <see cref="OrganTraumaWeightComponent"/>.
    /// </summary>
    public EntityUid? PickOrgan(IReadOnlyList<EntityUid> organs)
    {
        var total = 0f;
        foreach (var organ in organs)
            total += Weight(organ);

        var roll = _random.NextFloat(total);
        foreach (var organ in organs)
        {
            roll -= Weight(organ);
            if (roll < 0)
                return organ;
        }

        return organs.Count > 0 ? organs[^1] : null;
    }

    private float Weight(EntityUid organ)
    {
        return CompOrNull<OrganTraumaWeightComponent>(organ)?.Weight ?? 1f;
    }
}

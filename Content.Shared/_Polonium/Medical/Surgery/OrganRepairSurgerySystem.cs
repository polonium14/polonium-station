using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
using Content.Shared.FixedPoint;

namespace Content.Shared._Polonium.Medical.Surgery;

/// <summary>
/// Finds the organ the repair step works on. The repair itself is done on the server, see OrganRepairSystem.
/// </summary>
public sealed partial class OrganRepairSurgerySystem : EntitySystem
{
    /// <summary>
    /// The most damaged organ of the part that the step can still repair, with the integrity it would be brought to.
    /// </summary>
    public bool TryGetRepairable(EntityUid step, EntityUid part, out Entity<OrganIntegrityComponent> organ, out FixedPoint2 target)
    {
        organ = default;
        target = FixedPoint2.Zero;
        if (!TryComp<SurgeryRepairOrgansStepComponent>(step, out var repair)
            || !TryComp<OrganComponent>(part, out var partOrgan)
            || !TryComp<BodyComponent>(partOrgan.Body, out var body)
            || body.Organs is not { } organs)
            return false;

        var worst = 1f;
        foreach (var uid in organs.ContainedEntities)
        {
            if (CompOrNull<ChildOrganComponent>(uid)?.Parent != part
                || HasComp<OrganRepairedComponent>(uid)
                || !TryComp<OrganIntegrityComponent>(uid, out var integrity)
                || integrity.IntegrityCap <= FixedPoint2.Zero)
                continue;

            var health = (integrity.OrganIntegrity / integrity.IntegrityCap).Float();
            if (health >= repair.RepairTo || health >= worst)
                continue;

            worst = health;
            organ = (uid, integrity);
            target = integrity.IntegrityCap * repair.RepairTo;
        }

        return organ.Owner.IsValid();
    }

    /// <summary>
    /// The repair step is done once no organ of the part is left to repair.
    /// </summary>
    public bool IsStepComplete(EntityUid step, EntityUid part)
    {
        return !HasComp<SurgeryRepairOrgansStepComponent>(step) || !TryGetRepairable(step, part, out _, out _);
    }
}

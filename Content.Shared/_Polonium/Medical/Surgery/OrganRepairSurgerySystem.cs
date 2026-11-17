using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
using Content.Shared.FixedPoint;

namespace Content.Shared._Polonium.Medical.Surgery;

/// <summary>
/// Finds the organ a repair surgery works on. The repair itself is done on the server, see OrganRepairSystem.
/// </summary>
public sealed partial class OrganRepairSurgerySystem : EntitySystem
{
    /// <summary>
    /// The organ the surgery names, if the step can still repair it, with the integrity it would be brought to.
    /// </summary>
    public bool TryGetRepairable(EntityUid step, EntityUid body, EntityUid surgery, out Entity<OrganIntegrityComponent> organ, out FixedPoint2 target)
    {
        organ = default;
        target = FixedPoint2.Zero;
        if (!TryComp<SurgeryRepairOrganStepComponent>(step, out var repair)
            || !TryComp<SurgeryOrganConditionComponent>(surgery, out var condition)
            || !TryComp<BodyComponent>(body, out var bodyComp)
            || !LimbTargetMap.TryGetOrganByCategory(EntityManager, bodyComp, condition.Category, out var uid)
            || HasComp<OrganRepairedComponent>(uid)
            || !TryComp<OrganIntegrityComponent>(uid, out var integrity)
            || integrity.IntegrityCap <= FixedPoint2.Zero
            || (integrity.OrganIntegrity / integrity.IntegrityCap).Float() >= repair.RepairTo)
            return false;

        organ = (uid, integrity);
        target = integrity.IntegrityCap * repair.RepairTo;
        return true;
    }

    /// <summary>
    /// The repair step is done when the organ needs no repair, was repaired before or is dead.
    /// </summary>
    public bool IsStepComplete(EntityUid step, EntityUid body, EntityUid surgery)
    {
        return !HasComp<SurgeryRepairOrganStepComponent>(step) || !TryGetRepairable(step, body, surgery, out _, out _);
    }
}

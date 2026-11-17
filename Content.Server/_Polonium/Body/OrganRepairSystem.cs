using Content.Shared._Polonium.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;

namespace Content.Server._Polonium.Body;

/// <summary>
/// The organ repair surgery: one finished step brings one organ back to working order, once per organ.
/// </summary>
public sealed partial class OrganRepairSystem : EntitySystem
{
    [Dependency] private OrganHealingSystem _healing = default!;
    [Dependency] private OrganRepairSurgerySystem _repair = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryRepairOrgansStepComponent, SurgeryStepEvent>(OnRepairStep);
    }

    private void OnRepairStep(Entity<SurgeryRepairOrgansStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_repair.TryGetRepairable(ent, args.Part, out var organ, out var target))
            return;

        _healing.HealOrgan(organ, target - organ.Comp.OrganIntegrity);
        EnsureComp<OrganRepairedComponent>(organ);
    }
}

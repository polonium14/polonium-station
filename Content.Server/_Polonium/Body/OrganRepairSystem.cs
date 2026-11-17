using Content.Shared._Polonium.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;

namespace Content.Server._Polonium.Body;

/// <summary>
/// The organ repair surgeries: the finished step brings the chosen organ back to working order, once per organ.
/// </summary>
public sealed partial class OrganRepairSystem : EntitySystem
{
    [Dependency] private OrganHealingSystem _healing = default!;
    [Dependency] private OrganRepairSurgerySystem _repair = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryRepairOrganStepComponent, SurgeryStepEvent>(OnRepairStep);
    }

    private void OnRepairStep(Entity<SurgeryRepairOrganStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_repair.TryGetRepairable(ent, args.Body, args.Surgery, out var organ, out var target))
            return;

        _healing.HealOrgan(organ, target - organ.Comp.OrganIntegrity);
        EnsureComp<OrganRepairedComponent>(organ);
    }
}

using Content.Shared._Polonium.Body;
using Content.Shared._Polonium.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;

namespace Content.Server._Polonium.Body;

/// <summary>
/// The organ repair surgeries: the finished step brings the chosen organ back to working order
/// and ends its failure, once per organ.
/// </summary>
public sealed partial class OrganRepairSystem : EntitySystem
{
    [Dependency] private OrganFailureSystem _failure = default!;
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
        if (TryComp<OrganFailureComponent>(organ, out var failure))
            _failure.SetStage((organ, failure), 0);
        EnsureComp<OrganRepairedComponent>(organ);
    }
}

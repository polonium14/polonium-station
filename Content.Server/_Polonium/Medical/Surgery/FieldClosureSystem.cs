using Content.Server._Polonium.Medical.PatientIntake;
using Content.Shared._Polonium.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Medical.Surgery;

/// <summary>
/// Closing a sawed-open chest or skull without bone gel leaves the bone broken.
/// </summary>
public sealed partial class FieldClosureSystem : EntitySystem
{
    [Dependency] private PatientIntakeSystem _intake = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryFieldClosureStepComponent, SurgeryStepEvent>(OnFieldClosure);
    }

    private void OnFieldClosure(Entity<SurgeryFieldClosureStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp<WoundableComponent>(args.Part, out var woundable)
            || !TryComp<BoneComponent>(woundable.Bone?.ContainedEntities.FirstOrNull(), out var bone)
            || bone.BoneSeverity == BoneSeverity.Broken)
            return;

        // Through a real trauma, so the mend-bones surgery can treat it later.
        _intake.DamageBone((args.Part, woundable), bone.BoneIntegrity);
    }
}

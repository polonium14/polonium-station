using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Whitelist;

namespace Content.Shared._EinsteinEngines.StepTrigger;

public sealed partial class StepTriggerImmunitySystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StepTriggerComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
    }

    private void OnStepTriggerAttempt(Entity<StepTriggerComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (TryComp<StepTriggerImmuneComponent>(args.Tripper, out var immune) &&
            _whitelist.IsWhitelistPass(immune.Whitelist, ent))
        {
            args.Cancelled = true;
        }
    }
}

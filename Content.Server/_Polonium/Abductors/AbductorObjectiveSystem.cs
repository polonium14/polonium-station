using Content.Server.Objectives.Systems;
using Content.Shared.Objectives.Components;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorObjectiveSystem : EntitySystem
{
    [Dependency] private AbductorShipSystem _ship = default!;
    [Dependency] private NumberObjectiveSystem _number = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AbductorExperimentsConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<AbductorExperimentsConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var target = _number.GetTarget(ent);
        args.Progress = target > 0
            ? MathF.Min(1f, _ship.GetExperiments(args.MindId) / (float) target)
            : 1f;
    }
}

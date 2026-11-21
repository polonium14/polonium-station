using Content.Server.Lathe.Components;
using Content.Shared._Polonium.Lathe;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Lathe;
using Content.Shared.ReagentSpeed;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Lathe;

public sealed partial class LatheProgressSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheComponent, LatheStartPrintingEvent>(OnStartPrinting);
        // The lathe's own system holds the shutdown of this component, so the end of a run is caught on its removal.
        SubscribeLocalEvent<LatheProducingComponent, ComponentRemove>(OnRunEnd);
    }

    private void OnStartPrinting(Entity<LatheComponent> ent, ref LatheStartPrintingEvent args)
    {
        if (!TryComp<LatheProducingComponent>(ent, out var producing))
            return;

        var progress = EnsureComp<LatheProgressComponent>(ent);
        // Within a run only an item that came out starts the next one.
        if (progress.Recipe != null)
            progress.Finished = progress.StartTime;

        progress.Recipe = args.Recipe.ID;
        progress.StartTime = producing.StartTime;
        progress.Length = producing.ProductionLength;
        Dirty(ent, progress);

        // The lathe takes its lube straight out of the solution, which never reaches the clients on its own.
        if (TryComp<ReagentSpeedComponent>(ent, out var speed) && _solutions.TryGetSolution(ent.Owner, speed.Solution, out var lube, out _))
            Dirty(lube.Value);
    }

    private void OnRunEnd(Entity<LatheProducingComponent> ent, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (TryComp<LatheProgressComponent>(ent, out var progress) && progress.Recipe != null)
        {
            // An aborted item stops before its time is up.
            if (_timing.CurTime >= progress.StartTime + ent.Comp.ProductionLength)
                progress.Finished = progress.StartTime;

            progress.Recipe = null;
            Dirty(ent, progress);
        }

        var ev = new LatheRunEndedEvent();
        RaiseLocalEvent(ent, ref ev);
    }
}

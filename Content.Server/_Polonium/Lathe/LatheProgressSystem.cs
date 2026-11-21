using Content.Server.Lathe.Components;
using Content.Shared._Polonium.Lathe;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Lathe;
using Content.Shared.ReagentSpeed;

namespace Content.Server._Polonium.Lathe;

public sealed partial class LatheProgressSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheComponent, LatheStartPrintingEvent>(OnStartPrinting);
    }

    private void OnStartPrinting(Entity<LatheComponent> ent, ref LatheStartPrintingEvent args)
    {
        if (!TryComp<LatheProducingComponent>(ent, out var producing))
            return;

        var progress = EnsureComp<LatheProgressComponent>(ent);
        progress.Recipe = args.Recipe.ID;
        progress.StartTime = producing.StartTime;
        progress.Length = producing.ProductionLength;
        Dirty(ent, progress);

        // The lathe takes its lube straight out of the solution, which never reaches the clients on its own.
        if (TryComp<ReagentSpeedComponent>(ent, out var speed) && _solutions.TryGetSolution(ent.Owner, speed.Solution, out var lube, out _))
            Dirty(lube.Value);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // The lathe system raises no event when printing stops, it only drops its own component.
        var query = EntityQueryEnumerator<LatheProgressComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!HasComp<LatheProducingComponent>(uid))
                RemCompDeferred<LatheProgressComponent>(uid);
        }
    }
}

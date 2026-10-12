using Content.Server.Lathe.Components;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Lathe;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorDuplicatorSystem : EntitySystem
{
    [Dependency] private SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorDuplicatorComponent, LatheStartPrintingEvent>(OnStartPrinting);
        SubscribeLocalEvent<ReplicatorDuplicatorComponent, LatheGetResultEvent>(OnLatheResult);
        SubscribeLocalEvent<ReplicatorDuplicatorComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
    }

    private void OnStartPrinting(Entity<ReplicatorDuplicatorComponent> ent, ref LatheStartPrintingEvent args)
    {
        if (!TryComp<LatheProducingComponent>(ent, out var producing))
            return;

        var query = EntityQueryEnumerator<ReplicatorNestComponent>();
        while (query.MoveNext(out _, out var nest))
        {
            if (nest.CurrentLevel < ent.Comp.BoostNestLevel)
                continue;

            producing.ProductionLength *= ent.Comp.BoostTimeMultiplier;
            return;
        }
    }

    private void OnLatheResult(Entity<ReplicatorDuplicatorComponent> ent, ref LatheGetResultEvent args)
    {
        if (TryComp<StackComponent>(args.ResultItem, out var stack))
            _stack.SetCount((args.ResultItem, stack), ent.Comp.Output);
    }

    private void OnUiOpenAttempt(Entity<ReplicatorDuplicatorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<ReplicatorComponent>(args.User))
            args.Cancel();
    }
}

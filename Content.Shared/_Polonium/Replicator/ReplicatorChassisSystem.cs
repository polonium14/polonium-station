using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Whitelist;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorChassisSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorChassisComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ReplicatorChassisComponent, UserOpenActivatableUIAttemptEvent>(OnUiOpenAttempt);
    }

    private void OnMapInit(Entity<ReplicatorChassisComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream))
            return;

        foreach (var name in new[] { bloodstream.BloodSolutionName, bloodstream.BloodTemporarySolutionName, bloodstream.MetabolitesSolutionName })
        {
            if (_solutionContainer.TryGetSolution(ent.Owner, name, out var solution))
                _solutionContainer.RemoveAllSolution(solution.Value);
        }

        RemComp(ent, bloodstream);
    }

    private void OnUiOpenAttempt(Entity<ReplicatorChassisComponent> ent, ref UserOpenActivatableUIAttemptEvent args)
    {
        if (args.Target != ent.Owner && !_whitelist.IsWhitelistPass(ent.Comp.UiWhitelist, args.Target))
            args.Cancel();
    }
}

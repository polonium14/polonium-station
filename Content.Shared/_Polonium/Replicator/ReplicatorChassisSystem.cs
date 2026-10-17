using Content.Shared.Body.Components;
using Content.Shared.UserInterface;
using Content.Shared.Whitelist;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorChassisSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorChassisComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ReplicatorChassisComponent, UserOpenActivatableUIAttemptEvent>(OnUiOpenAttempt);
    }

    private void OnMapInit(Entity<ReplicatorChassisComponent> ent, ref MapInitEvent args)
    {
        // The silicon base fills it with flammable oil, and components cannot be dropped down the prototype chain.
        RemComp<BloodstreamComponent>(ent);
    }

    private void OnUiOpenAttempt(Entity<ReplicatorChassisComponent> ent, ref UserOpenActivatableUIAttemptEvent args)
    {
        if (args.Target != ent.Owner && !_whitelist.IsWhitelistPass(ent.Comp.UiWhitelist, args.Target))
            args.Cancel();
    }
}

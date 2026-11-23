using Content.Server.Lathe;
using Content.Shared._Polonium.Lathe;
using Content.Shared.Lathe;

namespace Content.Server._Polonium.Lathe;

public sealed partial class LatheStopSystem : EntitySystem
{
    [Dependency] private LatheSystem _lathe = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheComponent, LatheStopAllMessage>(OnStopAll);
        SubscribeLocalEvent<LatheStoppedComponent, LatheStartPrintingEvent>(OnStartPrinting);
    }

    private void OnStopAll(Entity<LatheComponent> ent, ref LatheStopAllMessage args)
    {
        // The lathe's own handlers refund the materials and write the admin log.
        // The queue goes first, because aborting the item in work starts the next one.
        while (ent.Comp.Queue.Count > 0)
        {
            var delete = new LatheDeleteRequestMessage(0) { Actor = args.Actor };
            _lathe.OnLatheDeleteRequestMessage(ent, ent.Comp, ref delete);
        }

        var abort = new LatheAbortFabricationMessage { Actor = args.Actor };
        _lathe.OnLatheAbortFabricationMessage(ent, ent.Comp, ref abort);

        EnsureComp<LatheStoppedComponent>(ent);
    }

    private void OnStartPrinting(Entity<LatheStoppedComponent> ent, ref LatheStartPrintingEvent args)
    {
        RemCompDeferred<LatheStoppedComponent>(ent);
    }
}

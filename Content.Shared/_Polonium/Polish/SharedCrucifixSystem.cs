using Content.Shared.Buckle.Components;

namespace Content.Shared._Polonium.Polish;

public abstract class SharedCrucifixSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrucifixComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<CrucifixComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
    }

    private void OnStrapAttempt(Entity<CrucifixComponent> ent, ref StrapAttemptEvent args)
    {
        if (!ent.Comp.Bypass)
            args.Cancelled = true;
    }

    private void OnUnstrapAttempt(Entity<CrucifixComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (args.User is not { } user)
            return;

        args.Cancelled = true;
        if (args.Popup)
            TryStartFreeing(ent, user, args.Buckle);
    }

    protected virtual void TryStartFreeing(Entity<CrucifixComponent> ent, EntityUid user, EntityUid victim)
    {
    }
}

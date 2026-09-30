using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorTowSystem : EntitySystem
{
    [Dependency] private ReplicatorHiveSystem _hive = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PullableComponent, TileFrictionEvent>(OnTileFriction);
        SubscribeLocalEvent<ReplicatorTowComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<ReplicatorTowComponent, PullStoppedMessage>(OnPullStopped);
        SubscribeLocalEvent<ReplicatorTowComponent, RefreshWeightlessModifiersEvent>(OnRefreshWeightless);
    }

    // Only while the replicator moves, or a stopped tow would keep sliding and drag it along.
    private void OnTileFriction(Entity<PullableComponent> ent, ref TileFrictionEvent args)
    {
        if (ent.Comp.Puller is not { } puller
            || !TryComp<ReplicatorTowComponent>(puller, out var tow)
            || !TryComp<InputMoverComponent>(puller, out var mover)
            || !mover.HasDirectionalMovement
            || !_hive.CanDigest(ent))
            return;

        args.Modifier *= tow.PulledFrictionModifier;
    }

    private void OnPullStarted(Entity<ReplicatorTowComponent> ent, ref PullStartedMessage args)
    {
        _movementSpeed.RefreshWeightlessModifiers(ent.Owner);
    }

    private void OnPullStopped(Entity<ReplicatorTowComponent> ent, ref PullStoppedMessage args)
    {
        _movementSpeed.RefreshWeightlessModifiers(ent.Owner);
    }

    private void OnRefreshWeightless(Entity<ReplicatorTowComponent> ent, ref RefreshWeightlessModifiersEvent args)
    {
        if (TryComp<PullerComponent>(ent, out var puller) && puller.Pulling != null)
            args.ModifyAcceleration(ent.Comp.WeightlessAccelerationModifier, 1f);
    }
}

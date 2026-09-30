using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorSheetHandSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorSheetHandComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<ReplicatorSheetHandComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    // The other hands always hold tools, so the empty sheet hand would otherwise unlock unarmed disarms.
    private void OnAttackAttempt(Entity<ReplicatorSheetHandComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Disarm && args.Weapon?.Owner == ent.Owner)
            args.Cancel();
    }

    private void OnMobStateChanged(Entity<ReplicatorSheetHandComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            _hands.TryDrop(ent.Owner, ent.Comp.HandId, checkActionBlocker: false);
    }

    public void TransferSheets(EntityUid from, EntityUid to)
    {
        if (!TryComp<ReplicatorSheetHandComponent>(from, out var fromHand)
            || !_hands.TryGetHeldItem(from, fromHand.HandId, out var sheets))
            return;

        if (TryComp<ReplicatorSheetHandComponent>(to, out var toHand)
            && _hands.TryPickup(to, sheets.Value, toHand.HandId, checkActionBlocker: false, animate: false))
            return;

        _hands.TryDrop(from, fromHand.HandId, checkActionBlocker: false);
    }
}

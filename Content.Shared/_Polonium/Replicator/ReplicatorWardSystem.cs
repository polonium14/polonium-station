using Content.Shared._Impstation.Replicator;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Projectiles;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorWardSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorWardComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<ReplicatorWardComponent, InventoryRelayedEvent<BeforeStaminaDamageEvent>>(OnStaminaDamage);
    }

    private void OnDamageModify(Entity<ReplicatorWardComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (IsReplicator(args.Args.Origin))
            args.Args.Damage = ReplicatorModuleSystem.ScaleHarm(args.Args.Damage, ent.Comp.DamageCoefficient);
    }

    private void OnStaminaDamage(Entity<ReplicatorWardComponent> ent, ref InventoryRelayedEvent<BeforeStaminaDamageEvent> args)
    {
        if (args.Args.Value > 0 && IsReplicator(args.Args.Source))
            args.Args.Value *= ent.Comp.StaminaCoefficient;
    }

    private bool IsReplicator(EntityUid? source)
    {
        if (TryComp<ProjectileComponent>(source, out var projectile))
            source = projectile.Shooter;

        return HasComp<ReplicatorComponent>(source);
    }
}

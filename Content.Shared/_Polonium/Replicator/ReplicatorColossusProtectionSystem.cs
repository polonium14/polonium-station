using Content.Shared._Impstation.Replicator;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Events;
using Content.Shared.Projectiles;
using Content.Shared.Pulling.Events;
using Content.Shared.StatusEffectNew;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorColossusProtectionSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorColossusComponent, BeforeStatusEffectAddedEvent>(OnStatusEffectAttempt);
        SubscribeLocalEvent<ReplicatorColossusComponent, BeforeStaminaDamageEvent>(OnStaminaDamageAttempt);
        SubscribeLocalEvent<ReplicatorColossusComponent, BeingPulledAttemptEvent>(OnPullAttempt);

        SubscribeLocalEvent<ReplicatorColossusLaunchComponent, ComponentStartup>(OnLaunchChanged);
        SubscribeLocalEvent<ReplicatorColossusLaunchComponent, ComponentShutdown>(OnLaunchChanged);
        SubscribeLocalEvent<ReplicatorColossusLaunchComponent, UpdateCanMoveEvent>(OnLaunchMoveAttempt);
        SubscribeLocalEvent<ReplicatorColossusLaunchComponent, ChangeDirectionAttemptEvent>(OnLaunchDirectionAttempt);
        SubscribeLocalEvent<ReplicatorColossusLaunchComponent, InteractionAttemptEvent>(OnLaunchInteractionAttempt);
        SubscribeLocalEvent<ReplicatorColossusLaunchComponent, AttackAttemptEvent>(OnLaunchAttackAttempt);
        SubscribeLocalEvent<ReplicatorColossusLaunchComponent, ShotAttemptedEvent>(OnLaunchShotAttempt);
        SubscribeLocalEvent<ReplicatorColossusLaunchComponent, BeforeDamageChangedEvent>(OnLaunchDamageAttempt);

        SubscribeLocalEvent<ReplicatorColossusLinkedComponent, ComponentStartup>(OnLinkedChanged);
        SubscribeLocalEvent<ReplicatorColossusLinkedComponent, ComponentShutdown>(OnLinkedChanged);
        SubscribeLocalEvent<ReplicatorColossusLinkedComponent, UpdateCanMoveEvent>(OnLinkedMoveAttempt);
        SubscribeLocalEvent<ReplicatorColossusLinkedComponent, ShotAttemptedEvent>(OnLinkedShotAttempt);

        SubscribeLocalEvent<ReplicatorColossusDisarmedComponent, ShotAttemptedEvent>(OnDisarmedShotAttempt);

        SubscribeLocalEvent<ReplicatorColossusProjectileComponent, PreventCollideEvent>(OnProjectileCollideAttempt);
        SubscribeLocalEvent<ReplicatorColossusProjectileComponent, BeforeProjectileHitEvent>(OnProjectileHit);
    }

    private void OnStatusEffectAttempt(Entity<ReplicatorColossusComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        args.Cancelled = true;
    }

    private void OnStaminaDamageAttempt(Entity<ReplicatorColossusComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        args.Cancelled = true;
    }

    private void OnPullAttempt(Entity<ReplicatorColossusComponent> ent, ref BeingPulledAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnLaunchChanged<T>(Entity<ReplicatorColossusLaunchComponent> ent, ref T args)
    {
        _actionBlocker.UpdateCanMove(ent);
    }

    private void OnLaunchMoveAttempt(Entity<ReplicatorColossusLaunchComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    private void OnLaunchDirectionAttempt(Entity<ReplicatorColossusLaunchComponent> ent, ref ChangeDirectionAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnLaunchInteractionAttempt(Entity<ReplicatorColossusLaunchComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnLaunchAttackAttempt(Entity<ReplicatorColossusLaunchComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnLaunchShotAttempt(Entity<ReplicatorColossusLaunchComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
    }

    private void OnLaunchDamageAttempt(Entity<ReplicatorColossusLaunchComponent> ent, ref BeforeDamageChangedEvent args)
    {
        args.Cancelled = true;
    }

    private void OnLinkedChanged<T>(Entity<ReplicatorColossusLinkedComponent> ent, ref T args)
    {
        _actionBlocker.UpdateCanMove(ent);
    }

    private void OnLinkedMoveAttempt(Entity<ReplicatorColossusLinkedComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    private void OnLinkedShotAttempt(Entity<ReplicatorColossusLinkedComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
    }

    private void OnDisarmedShotAttempt(Entity<ReplicatorColossusDisarmedComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
    }

    private void OnProjectileCollideAttempt(Entity<ReplicatorColossusProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if (HasComp<ReplicatorComponent>(args.OtherEntity) || HasComp<ReplicatorNestComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnProjectileHit(Entity<ReplicatorColossusProjectileComponent> ent, ref BeforeProjectileHitEvent args)
    {
        if (_tag.HasTag(args.Target, ReplicatorHiveSystem.StructureTag))
            args.Damage = new DamageSpecifier();
    }
}

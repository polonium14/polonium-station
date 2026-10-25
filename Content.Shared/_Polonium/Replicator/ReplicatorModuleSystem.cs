using System.Diagnostics.CodeAnalysis;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Prying.Components;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorModuleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorModulesComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<ReplicatorModulesComponent, GetPryTimeModifierEvent>(OnGetPryTime);
        SubscribeLocalEvent<ReplicatorModulesComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<ReplicatorModulesComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<ReplicatorModulesComponent, MeleeAttackEvent>(OnMeleeAttack);
        SubscribeLocalEvent<ReplicatorModulesComponent, SelfBeforeGunShotEvent>(OnGunShot);
        SubscribeLocalEvent<ReplicatorModulesComponent, ReplicatorDashActionEvent>(OnDash);
        SubscribeLocalEvent<ReplicatorGunBoostComponent, GunRefreshModifiersEvent>(OnRefreshGun);
        SubscribeLocalEvent<ReplicatorSprintRevealComponent, MoveEvent>(OnSprintMove);
    }

    public bool TryGetClass(Entity<ReplicatorModulesComponent?> ent, [NotNullWhen(true)] out ReplicatorClassPrototype? prototype)
    {
        prototype = null;
        return Resolve(ent, ref ent.Comp, false) && ent.Comp.Class is { } id && _prototype.TryIndex(id, out prototype);
    }

    public int GetStage(Entity<ReplicatorModulesComponent?> ent, ProtoId<ReplicatorModulePrototype> module)
    {
        return Resolve(ent, ref ent.Comp, false) ? ent.Comp.Stages.GetValueOrDefault(module) : 0;
    }

    public int GetTotalStages(ReplicatorModulesComponent modules)
    {
        var total = 0;
        foreach (var stage in modules.Stages.Values)
        {
            total += stage;
        }

        return total;
    }

    public IEnumerable<ReplicatorModuleStage> EnumerateStages(Entity<ReplicatorModulesComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            yield break;

        foreach (var (id, stage) in ent.Comp.Stages)
        {
            if (stage > 0 && _prototype.TryIndex(id, out var module) && stage <= module.Stages.Count)
                yield return module.Stages[stage - 1];
        }
    }

    public float GetSpeedMultiplier(EntityUid uid)
    {
        var multiplier = Product(uid, static stage => stage.SpeedMultiplier);
        return TryGetClass(uid, out var prototype) ? multiplier * prototype.SpeedMultiplier : multiplier;
    }

    public float GetFireRateMultiplier(EntityUid uid)
    {
        return Product(uid, static stage => stage.FireRateMultiplier);
    }

    public int GetGunCapacityBonus(EntityUid uid)
    {
        var bonus = 0;
        foreach (var stage in EnumerateStages(uid))
        {
            bonus += stage.GunCapacityBonus;
        }

        return bonus;
    }

    public float GetGunRechargeMultiplier(EntityUid uid)
    {
        return Product(uid, static stage => stage.GunRechargeMultiplier);
    }

    public float GetBuildTimeMultiplier(EntityUid uid)
    {
        return Product(uid, static stage => stage.BuildTimeMultiplier);
    }

    public float GetTeleportDelayMultiplier(EntityUid uid)
    {
        return Product(uid, static stage => stage.TeleportDelayMultiplier);
    }

    public float GetTowFrictionMultiplier(EntityUid uid)
    {
        return Product(uid, static stage => stage.TowFrictionMultiplier);
    }

    public float GetTowAccelerationMultiplier(EntityUid uid)
    {
        return Product(uid, static stage => stage.TowAccelerationMultiplier);
    }

    public float GetTeleportImmunityMultiplier(EntityUid uid)
    {
        return Product(uid, static stage => stage.TeleportImmunityMultiplier);
    }

    public TimeSpan GetTeleportParalyzeBonus(EntityUid uid)
    {
        var bonus = TimeSpan.Zero;
        foreach (var stage in EnumerateStages(uid))
        {
            bonus += stage.TeleportParalyzeBonus;
        }

        return bonus;
    }

    public bool HasFloorSpread(EntityUid uid)
    {
        foreach (var stage in EnumerateStages(uid))
        {
            if (stage.FloorSpread)
                return true;
        }

        return false;
    }

    private float Product(EntityUid uid, Func<ReplicatorModuleStage, float> selector)
    {
        var result = 1f;
        foreach (var stage in EnumerateStages(uid))
        {
            result *= selector(stage);
        }

        return result;
    }

    private void OnGetPryTime(Entity<ReplicatorModulesComponent> ent, ref GetPryTimeModifierEvent args)
    {
        args.PryTimeModifier *= MathF.Pow(ent.Comp.PryTimePerStage, GetTotalStages(ent.Comp));
    }

    private void OnRefreshSpeed(Entity<ReplicatorModulesComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var multiplier = GetSpeedMultiplier(ent);
        args.ModifySpeed(multiplier, multiplier);
    }

    private void OnDamageModify(Entity<ReplicatorModulesComponent> ent, ref DamageModifyEvent args)
    {
        var coefficient = Product(ent, static stage => stage.DamageCoefficient);
        if (coefficient != 1f)
            args.Damage = ScaleHarm(args.Damage, coefficient);
    }

    public static DamageSpecifier ScaleHarm(DamageSpecifier damage, float coefficient)
    {
        var scaled = new DamageSpecifier(damage);
        foreach (var (type, value) in damage.DamageDict)
        {
            if (value > 0)
                scaled.DamageDict[type] = value * coefficient;
        }

        return scaled;
    }

    private void OnGetMeleeDamage(Entity<ReplicatorModulesComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (args.Weapon != ent.Owner)
            return;

        foreach (var stage in EnumerateStages(ent.AsNullable()))
        {
            if (stage.MeleeBonus != null)
                args.Damage += stage.MeleeBonus;
        }
    }

    private void OnMeleeAttack(Entity<ReplicatorModulesComponent> ent, ref MeleeAttackEvent args)
    {
        Reveal(ent);
    }

    private void OnGunShot(Entity<ReplicatorModulesComponent> ent, ref SelfBeforeGunShotEvent args)
    {
        Reveal(ent);
    }

    public void Reveal(EntityUid uid)
    {
        if (TryComp<StealthComponent>(uid, out var stealth))
            _stealth.SetVisibility(uid, 1f, stealth);
    }

    private void OnDash(Entity<ReplicatorModulesComponent> ent, ref ReplicatorDashActionEvent args)
    {
        if (args.Handled || !TryComp<PhysicsComponent>(ent, out var physics))
            return;

        var direction = physics.LinearVelocity.LengthSquared() > 0.01f
            ? physics.LinearVelocity.Normalized()
            : _transform.GetWorldRotation(ent).ToWorldVec();

        _physics.SetLinearVelocity(ent, direction * args.Speed, body: physics);
        _audio.PlayPredicted(args.Sound, ent, ent);
        args.Handled = true;
    }

    private void OnRefreshGun(Entity<ReplicatorGunBoostComponent> ent, ref GunRefreshModifiersEvent args)
    {
        args.FireRate *= ent.Comp.FireRateMultiplier;
    }

    private void OnSprintMove(Entity<ReplicatorSprintRevealComponent> ent, ref MoveEvent args)
    {
        if (_timing.ApplyingState || args.NewPosition.EntityId != args.OldPosition.EntityId)
            return;

        if (!TryComp<InputMoverComponent>(ent, out var mover) || !mover.Sprinting || !mover.HasDirectionalMovement)
            return;

        var distance = (args.NewPosition.Position - args.OldPosition.Position).Length();
        _stealth.ModifyVisibility(ent, ent.Comp.VisibilityRate * distance);
    }
}

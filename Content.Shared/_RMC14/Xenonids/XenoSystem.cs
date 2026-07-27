using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Pheromones;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Radio;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids;

public sealed partial class XenoSystem : EntitySystem
{
    private const float PlasmaRegenMultiplier = 5f;

    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private SharedXenoWeedsSystem _weeds = default!;

    private EntityQuery<DamageableComponent> _damageableQuery;
    private EntityQuery<XenoFriendlyComponent> _xenoFriendlyQuery;
    private EntityQuery<XenoPlasmaComponent> _xenoPlasmaQuery;
    private EntityQuery<XenoRecoveryPheromonesComponent> _xenoRecoveryQuery;
    private EntityQuery<XenoRestingComponent> _xenoRestingQuery;

    private float _xenoDamageDealtMultiplier = 1f;
    private float _xenoDamageReceivedMultiplier = 1f;
    private float _xenoSpeedMultiplier = 1f;

    public override void Initialize()
    {
        base.Initialize();

        _damageableQuery = GetEntityQuery<DamageableComponent>();
        _xenoFriendlyQuery = GetEntityQuery<XenoFriendlyComponent>();
        _xenoPlasmaQuery = GetEntityQuery<XenoPlasmaComponent>();
        _xenoRecoveryQuery = GetEntityQuery<XenoRecoveryPheromonesComponent>();
        _xenoRestingQuery = GetEntityQuery<XenoRestingComponent>();

        SubscribeLocalEvent<XenoComponent, MapInitEvent>(OnXenoMapInit);
        SubscribeLocalEvent<XenoComponent, NewXenoEvolvedEvent>(OnNewXenoEvolved);
        SubscribeLocalEvent<XenoComponent, XenoDevolvedEvent>(OnXenoDevolved);
        SubscribeLocalEvent<XenoComponent, GetDefaultRadioChannelEvent>(OnXenoGetDefaultRadioChannel);
        SubscribeLocalEvent<XenoComponent, AttackAttemptEvent>(OnXenoAttackAttempt);
        SubscribeLocalEvent<XenoComponent, PickupAttemptEvent>(OnXenoPickupAttempt);
        SubscribeLocalEvent<XenoComponent, GetMeleeDamageEvent>(OnXenoGetMeleeDamage);
        SubscribeLocalEvent<XenoComponent, DamageModifyEvent>(OnXenoDamageModify);
        SubscribeLocalEvent<XenoComponent, RefreshMovementSpeedModifiersEvent>(OnXenoRefreshSpeed);

        SubscribeLocalEvent<XenoRegenComponent, MapInitEvent>(OnXenoRegenMapInit);

        SubscribeLocalEvent<XenoStateVisualsComponent, MobStateChangedEvent>(OnVisualsMobStateChanged);
        SubscribeLocalEvent<XenoStateVisualsComponent, XenoRestEvent>(OnVisualsRest);

        Subs.CVar(_config, RMCCVars.RMCXenoDamageDealtMultiplier, v => _xenoDamageDealtMultiplier = v, true);
        Subs.CVar(_config, RMCCVars.RMCXenoDamageReceivedMultiplier, v => _xenoDamageReceivedMultiplier = v, true);
        Subs.CVar(_config, RMCCVars.RMCXenoSpeedMultiplier, UpdateXenoSpeedMultiplier, true);
    }

    private void OnXenoMapInit(Entity<XenoComponent> xeno, ref MapInitEvent args)
    {
        foreach (var actionId in xeno.Comp.ActionIds)
        {
            if (!xeno.Comp.Actions.ContainsKey(actionId) &&
                _action.AddAction(xeno, actionId) is { } newAction)
            {
                xeno.Comp.Actions[actionId] = newAction;
            }
        }

        if (!MathHelper.CloseTo(_xenoSpeedMultiplier, 1))
            _movementSpeed.RefreshMovementSpeedModifiers(xeno.Owner);

        Dirty(xeno);
    }

    private void OnNewXenoEvolved(Entity<XenoComponent> newXeno, ref NewXenoEvolvedEvent args)
    {
        var oldRotation = _transform.GetWorldRotation(args.OldXeno);
        _transform.SetWorldRotation(newXeno, oldRotation);
    }

    private void OnXenoDevolved(Entity<XenoComponent> newXeno, ref XenoDevolvedEvent args)
    {
        var oldRotation = _transform.GetWorldRotation(args.OldXeno);
        _transform.SetWorldRotation(newXeno, oldRotation);
    }

    private void OnXenoGetDefaultRadioChannel(Entity<XenoComponent> ent, ref GetDefaultRadioChannelEvent args)
    {
        args.Channel = "Hivemind";
    }

    private void OnXenoAttackAttempt(Entity<XenoComponent> xeno, ref AttackAttemptEvent args)
    {
        if (args.Target is not { } target)
            return;

        if ((_xenoFriendlyQuery.HasComp(target) && _hive.FromSameHive(xeno.Owner, target)) ||
            _mobState.IsDead(target))
        {
            if (!args.Disarm)
                args.Cancel();
        }
    }

    private void OnXenoPickupAttempt(Entity<XenoComponent> xeno, ref PickupAttemptEvent args)
    {
        if (args.User != xeno.Owner)
            return;

        args.Cancel();
    }

    private void OnXenoGetMeleeDamage(Entity<XenoComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (MathHelper.CloseTo(_xenoDamageDealtMultiplier, 1))
            return;

        args.Damage *= _xenoDamageDealtMultiplier;
    }

    private void OnXenoDamageModify(Entity<XenoComponent> ent, ref DamageModifyEvent args)
    {
        if (MathHelper.CloseTo(_xenoDamageReceivedMultiplier, 1))
            return;

        args.Damage *= _xenoDamageReceivedMultiplier;
    }

    private void OnXenoRefreshSpeed(Entity<XenoComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (MathHelper.CloseTo(_xenoSpeedMultiplier, 1))
            return;

        args.ModifySpeed(_xenoSpeedMultiplier, _xenoSpeedMultiplier);
    }

    private void OnXenoRegenMapInit(Entity<XenoRegenComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextRegen = _timing.CurTime + ent.Comp.RegenCooldown;
        Dirty(ent);
    }

    private void OnVisualsMobStateChanged(Entity<XenoStateVisualsComponent> ent, ref MobStateChangedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        _appearance.SetData(ent, RMCXenoStateVisuals.Downed, args.NewMobState != MobState.Alive);
        _appearance.SetData(ent, RMCXenoStateVisuals.Dead, args.NewMobState == MobState.Dead);
    }

    private void OnVisualsRest(Entity<XenoStateVisualsComponent> ent, ref XenoRestEvent args)
    {
        if (_timing.ApplyingState)
            return;

        _appearance.SetData(ent, RMCXenoStateVisuals.Resting, args.Resting);
    }

    private void UpdateXenoSpeedMultiplier(float speed)
    {
        _xenoSpeedMultiplier = speed;

        var xenos = EntityQueryEnumerator<XenoComponent>();
        while (xenos.MoveNext(out var uid, out _))
            _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    public void MakeXeno(EntityUid uid)
    {
        EnsureComp<XenoComponent>(uid);
    }

    public bool CanHeal(EntityUid xeno)
    {
        var ev = new XenoHealAttemptEvent();
        RaiseLocalEvent(xeno, ev);
        return !ev.Cancelled;
    }

    private FixedPoint2 GetWeedsHealAmount(Entity<XenoRegenComponent> xeno)
    {
        FixedPoint2 multiplier;
        if (_mobState.IsCritical(xeno))
            multiplier = xeno.Comp.CritHealMultiplier;
        else if (_xenoRestingQuery.HasComp(xeno))
            multiplier = xeno.Comp.RestHealMultiplier;
        else
            multiplier = xeno.Comp.StandHealingMultiplier;

        var recovery = CompOrNull<XenoRecoveryPheromonesComponent>(xeno)?.Multiplier ?? FixedPoint2.Zero;
        if (!CanHeal(xeno))
            recovery = FixedPoint2.Zero;

        var recoveryHeal = xeno.Comp.FlatHealing * (recovery / 2);
        return (xeno.Comp.FlatHealing + recoveryHeal) * multiplier;
    }

    public void HealDamage(EntityUid xeno, FixedPoint2 amount)
    {
        if (!CanHeal(xeno))
            return;

        if (!_damageableQuery.TryComp(xeno, out var damageable) ||
            _damageable.GetTotalDamage((xeno, damageable)) <= FixedPoint2.Zero)
        {
            return;
        }

        if (_mobState.IsDead(xeno))
            return;

        // brute + burn style heal - types clamp if entity doesnt have them
        var heal = new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = -amount,
                ["Slash"] = -amount,
                ["Piercing"] = -amount,
                ["Heat"] = -amount,
                ["Shock"] = -amount,
                ["Cold"] = -amount,
                ["Caustic"] = -amount,
            },
        };

        _damageable.TryChangeDamage(xeno, heal, true, false, origin: xeno);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoRegenComponent>();
        while (query.MoveNext(out var uid, out var regen))
        {
            if (time < regen.NextRegen)
                continue;

            regen.NextRegen = time + regen.RegenCooldown;
            Dirty(uid, regen);

            var onWeeds = _weeds.IsOnFriendlyWeeds(uid);
            if (!regen.HealOffWeeds && !onWeeds)
            {
                if (_xenoPlasmaQuery.TryComp(uid, out var plasmaOff))
                {
                    var amount = FixedPoint2.Max(plasmaOff.PlasmaRegenOffWeeds * plasmaOff.MaxPlasma / 100 / 2 * PlasmaRegenMultiplier, 0.01);
                    _xenoPlasma.RegenPlasma((uid, plasmaOff), amount);
                }

                continue;
            }

            var heal = GetWeedsHealAmount((uid, regen));
            if (heal > FixedPoint2.Zero)
                HealDamage(uid, heal);

            if (_xenoPlasmaQuery.TryComp(uid, out var plasma))
            {
                var plasmaRestored = plasma.PlasmaRegenOnWeeds * plasma.MaxPlasma / 100 / 2 * PlasmaRegenMultiplier;
                _xenoPlasma.RegenPlasma((uid, plasma), plasmaRestored);

                if (_xenoRecoveryQuery.TryComp(uid, out var recovery))
                {
                    var amount = plasmaRestored * recovery.Multiplier / 4;
                    _xenoPlasma.RegenPlasma((uid, plasma), amount);
                }
            }
        }
    }
}

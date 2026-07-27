using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Collections;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Pheromones;

public sealed class XenoPheromonesSystem : EntitySystem
{
    private static readonly EntProtoId PheromonesAction = "ActionXenoPheromones";

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private XenoPlasmaSystem _plasma = default!;

    private readonly TimeSpan _plasmaUseDelay = TimeSpan.FromSeconds(1);
    private readonly HashSet<EntityUid> _nearby = new();
    private readonly HashSet<EntityUid> _oldRecovery = new();
    private readonly HashSet<EntityUid> _oldWarding = new();
    private readonly HashSet<EntityUid> _oldFrenzy = new();
    private readonly HashSet<EntityUid> _refreshSpeeds = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoPheromonesComponent, XenoPheromonesActionEvent>(OnPheromonesAction);
        SubscribeLocalEvent<XenoPheromonesComponent, PlayerDetachedEvent>(OnDetached);

        SubscribeLocalEvent<XenoActivePheromonesComponent, MobStateChangedEvent>(OnActiveMobStateChanged);

        SubscribeLocalEvent<XenoFrenzyPheromonesComponent, ComponentRemove>(OnFrenzyRemove);
        SubscribeLocalEvent<XenoFrenzyPheromonesComponent, GetMeleeDamageEvent>(OnFrenzyGetMeleeDamage);
        SubscribeLocalEvent<XenoFrenzyPheromonesComponent, RefreshMovementSpeedModifiersEvent>(OnFrenzySpeed);

        SubscribeLocalEvent<XenoWardingPheromonesComponent, DamageModifyEvent>(OnWardingDamageModify);

        Subs.BuiEvents<XenoPheromonesComponent>(XenoPheromonesUI.Key, subs =>
        {
            subs.Event<XenoPheromonesChosenBuiMsg>(OnChosen);
        });
    }

    private void OnPheromonesAction(Entity<XenoPheromonesComponent> xeno, ref XenoPheromonesActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        args.Handled = true;
        DeactivatePheromones(xeno.Owner);
        _ui.TryOpenUi(xeno.Owner, XenoPheromonesUI.Key, xeno);
    }

    private void OnDetached(Entity<XenoPheromonesComponent> xeno, ref PlayerDetachedEvent args)
    {
        DeactivatePheromones(xeno.Owner);
    }

    private void OnActiveMobStateChanged(Entity<XenoActivePheromonesComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            DeactivatePheromones(ent.Owner);
    }

    private void OnChosen(Entity<XenoPheromonesComponent> xeno, ref XenoPheromonesChosenBuiMsg args)
    {
        if (!Enum.IsDefined(args.Pheromones))
            return;

        if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PheromonesPlasmaCost))
            return;

        SetPheromonesActionToggled(xeno.Owner, true);

        _popup.PopupClient(
            Loc.GetString("cm-xeno-pheromones-start", ("pheromones", Loc.GetString($"cm-pheromones-{args.Pheromones.ToString().ToLowerInvariant()}"))),
            xeno,
            xeno);

        _ui.CloseUi(xeno.Owner, XenoPheromonesUI.Key, xeno);

        if (_net.IsClient)
            return;

        xeno.Comp.NextPheromonesPlasmaUse = _timing.CurTime + _plasmaUseDelay;
        Dirty(xeno);

        var active = EnsureComp<XenoActivePheromonesComponent>(xeno);
        active.Pheromones = args.Pheromones;
        Dirty(xeno, active);

        RefreshReceivers(xeno.Owner, xeno.Comp, active);
    }

    private void OnFrenzyRemove(Entity<XenoFrenzyPheromonesComponent> ent, ref ComponentRemove args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnFrenzyGetMeleeDamage(Entity<XenoFrenzyPheromonesComponent> frenzy, ref GetMeleeDamageEvent args)
    {
        var bonus = frenzy.Comp.AttackDamageAddPerMult * frenzy.Comp.Multiplier.Float();
        args.Damage += new DamageSpecifier
        {
            DamageDict =
            {
                ["Slash"] = bonus,
                ["Blunt"] = bonus,
            },
        };
    }

    private void OnFrenzySpeed(Entity<XenoFrenzyPheromonesComponent> frenzy, ref RefreshMovementSpeedModifiersEvent args)
    {
        var speed = 1 + (frenzy.Comp.MovementSpeedModifier * frenzy.Comp.Multiplier).Float();
        args.ModifySpeed(speed, speed);
    }

    // softens crit hits only - no full RMC grace stack
    private void OnWardingDamageModify(Entity<XenoWardingPheromonesComponent> warding, ref DamageModifyEvent args)
    {
        if (!_mobState.IsCritical(warding) || args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        var factor = 1f / (1f + 0.25f * warding.Comp.Multiplier.Float());
        args.Damage *= factor;
    }

    public void DeactivatePheromones(EntityUid xeno)
    {
        SetPheromonesActionToggled(xeno, false);

        if (!HasComp<XenoActivePheromonesComponent>(xeno))
            return;

        if (_net.IsServer)
            RemComp<XenoActivePheromonesComponent>(xeno);

        _popup.PopupClient(Loc.GetString("cm-xeno-pheromones-stop"), xeno, xeno);
    }

    private void SetPheromonesActionToggled(EntityUid xeno, bool toggled)
    {
        if (!TryComp(xeno, out XenoComponent? xenoComp))
            return;

        if (!xenoComp.Actions.TryGetValue(PheromonesAction, out var action))
            return;

        _actions.SetToggled(action, toggled);
    }

    private void RefreshReceivers(EntityUid uid, XenoPheromonesComponent pheromones, XenoActivePheromonesComponent active)
    {
        active.Receivers.Clear();
        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(uid).Coordinates, pheromones.PheromonesRange, _nearby);

        foreach (var nearby in _nearby)
        {
            if (!HasComp<XenoComponent>(nearby))
                continue;

            if (!_hive.FromSameHive(uid, nearby))
                continue;

            if (_mobState.IsDead(nearby))
                continue;

            active.Receivers.Add(nearby);
        }
    }

    private static void AssignMaxMultiplier(ref FixedPoint2 current, FixedPoint2 next)
    {
        current = FixedPoint2.Max(current, next);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        _oldRecovery.Clear();
        var recoveryQuery = EntityQueryEnumerator<XenoRecoveryPheromonesComponent>();
        while (recoveryQuery.MoveNext(out var uid, out var recovery))
        {
            _oldRecovery.Add(uid);
            recovery.Multiplier = 0;
        }

        _oldWarding.Clear();
        var wardingQuery = EntityQueryEnumerator<XenoWardingPheromonesComponent>();
        while (wardingQuery.MoveNext(out var uid, out var warding))
        {
            _oldWarding.Add(uid);
            warding.Multiplier = 0;
        }

        _oldFrenzy.Clear();
        var frenzyQuery = EntityQueryEnumerator<XenoFrenzyPheromonesComponent>();
        while (frenzyQuery.MoveNext(out var uid, out var frenzy))
        {
            _oldFrenzy.Add(uid);
            frenzy.Multiplier = 0;
        }

        _refreshSpeeds.Clear();

        var toDeactivate = new ValueList<EntityUid>();

        var query = EntityQueryEnumerator<XenoActivePheromonesComponent, XenoPheromonesComponent>();
        while (query.MoveNext(out var uid, out var active, out var pheromones))
        {
            if (pheromones.PheromonesPlasmaUpkeep > 0 &&
                (!TryComp(uid, out XenoPlasmaComponent? plasmaComp) ||
                 !_plasma.HasPlasma((uid, plasmaComp), pheromones.PheromonesPlasmaUpkeep)))
            {
                toDeactivate.Add(uid);
                continue;
            }

            if (_timing.CurTime >= pheromones.NextPheromonesPlasmaUse)
            {
                pheromones.NextPheromonesPlasmaUse = _timing.CurTime + _plasmaUseDelay;
                Dirty(uid, pheromones);

                if (pheromones.PheromonesPlasmaUpkeep > 0 &&
                    !_plasma.TryRemovePlasma(uid, pheromones.PheromonesPlasmaUpkeep))
                {
                    toDeactivate.Add(uid);
                    continue;
                }

                RefreshReceivers(uid, pheromones, active);
            }

            switch (active.Pheromones)
            {
                case XenoPheromones.Recovery:
                    foreach (var receiver in active.Receivers)
                    {
                        if (Deleted(receiver) || _mobState.IsDead(receiver))
                            continue;

                        _oldRecovery.Remove(receiver);
                        var recovery = EnsureComp<XenoRecoveryPheromonesComponent>(receiver);
                        AssignMaxMultiplier(ref recovery.Multiplier, pheromones.PheromonesMultiplier);
                        Dirty(receiver, recovery);
                    }
                    break;

                case XenoPheromones.Warding:
                    foreach (var receiver in active.Receivers)
                    {
                        if (Deleted(receiver) || _mobState.IsDead(receiver))
                            continue;

                        _oldWarding.Remove(receiver);
                        var warding = EnsureComp<XenoWardingPheromonesComponent>(receiver);
                        AssignMaxMultiplier(ref warding.Multiplier, pheromones.PheromonesMultiplier);
                        Dirty(receiver, warding);
                    }
                    break;

                case XenoPheromones.Frenzy:
                    foreach (var receiver in active.Receivers)
                    {
                        if (Deleted(receiver) || _mobState.IsDead(receiver))
                            continue;

                        _oldFrenzy.Remove(receiver);
                        var frenzy = EnsureComp<XenoFrenzyPheromonesComponent>(receiver);
                        var old = frenzy.Multiplier;
                        AssignMaxMultiplier(ref frenzy.Multiplier, pheromones.PheromonesMultiplier);
                        Dirty(receiver, frenzy);

                        if (frenzy.Multiplier != old)
                            _refreshSpeeds.Add(receiver);
                    }
                    break;
            }
        }

        foreach (var uid in toDeactivate)
            DeactivatePheromones(uid);

        foreach (var uid in _refreshSpeeds)
            _movement.RefreshMovementSpeedModifiers(uid);

        foreach (var uid in _oldRecovery)
            RemComp<XenoRecoveryPheromonesComponent>(uid);

        foreach (var uid in _oldWarding)
            RemComp<XenoWardingPheromonesComponent>(uid);

        foreach (var uid in _oldFrenzy)
            RemComp<XenoFrenzyPheromonesComponent>(uid);
    }
}

using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared.Database;
using Content.Shared.Administration.Logs;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Polonium.Horror;

/// <summary>
/// The tentacle a horror grows on its host: a two tile whip that grabs, throws, disarms and pries airlocks.
/// Out of combat mode a click on someone grabs them, a click anywhere while pulling someone throws them,
/// a wide swing knocks items out of hands and a click on an airlock pries it open.
/// </summary>
public sealed partial class HorrorTentacleSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HorrorTentacleComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HorrorTentacleComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<HorrorTentacleComponent, HorrorPryDoAfterEvent>(OnPryDoAfter);
    }

    private void OnAfterInteract(Entity<HorrorTentacleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;

        // throw whoever the host is pulling at the clicked spot
        if (TryComp<PullerComponent>(user, out var puller)
            && puller.Pulling is { } pulled
            && pulled != args.Target
            && HasComp<MobStateComponent>(pulled))
        {
            if (TryComp<PullableComponent>(pulled, out var pullable))
                _pulling.TryStopPull(pulled, pullable, user);

            _throwing.TryThrow(pulled, args.ClickLocation, ent.Comp.ThrowSpeed, user);
            _stun.TryKnockdown(pulled, ent.Comp.GrabKnockdown, true, force: true);
            _popup.PopupEntity(Loc.GetString("horror-tentacle-throw",
                ("user", Identity.Entity(user, EntityManager)), ("target", Identity.Entity(pulled, EntityManager))), user, PopupType.MediumCaution);
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):user} threw {ToPrettyString(pulled):target} with a horror tentacle");
            args.Handled = true;
            return;
        }

        // everything below needs the target within the tentacle's reach, not just on screen
        if (args.Target is not { } target
            || target == user
            || !_interaction.InRangeUnobstructed(user, target, ent.Comp.Reach))
            return;

        if (TryComp<DoorComponent>(target, out var door))
        {
            args.Handled = true;
            TryPry(ent, user, (target, door));
            return;
        }

        if (!HasComp<MobStateComponent>(target) || !_pulling.TryStartPull(user, target))
            return;

        _stun.TryKnockdown(target, ent.Comp.GrabKnockdown, true, force: true);
        _popup.PopupEntity(Loc.GetString("horror-tentacle-grab",
            ("user", Identity.Entity(user, EntityManager)), ("target", Identity.Entity(target, EntityManager))), user, PopupType.MediumCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):user} grabbed {ToPrettyString(target):target} with a horror tentacle");
        args.Handled = true;
    }

    private void TryPry(Entity<HorrorTentacleComponent> ent, EntityUid user, Entity<DoorComponent> door)
    {
        if (door.Comp.State != DoorState.Closed)
            return;

        if (TryComp<DoorBoltComponent>(door, out var bolts) && bolts.BoltsDown)
        {
            _popup.PopupEntity(Loc.GetString("horror-tentacle-bolted"), door, user, PopupType.SmallCaution);
            return;
        }

        // an unpowered airlock gives way at once
        if (!_power.IsPowered(door.Owner))
        {
            _door.StartOpening(door, door.Comp, user);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.PryTime, new HorrorPryDoAfterEvent(), ent, door, ent)
        {
            DistanceThreshold = ent.Comp.Reach,
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("horror-tentacle-pry-start", ("user", Identity.Entity(user, EntityManager))), door, PopupType.MediumCaution);
    }

    private void OnPryDoAfter(Entity<HorrorTentacleComponent> ent, ref HorrorPryDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target || !TryComp<DoorComponent>(target, out var door))
            return;

        if (TryComp<DoorBoltComponent>(target, out var bolts) && bolts.BoltsDown)
            return;

        _door.StartOpening(target, door, args.User);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):user} pried open {ToPrettyString(target):door} with a horror tentacle");
        args.Handled = true;
    }

    /// <summary>
    /// A wide swing of the tentacle knocks what the victims hold out of their hands.
    /// </summary>
    private void OnMeleeHit(Entity<HorrorTentacleComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.Direction == null)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!_hands.TryGetActiveItem(hit, out var item) || !_hands.TryDrop(hit, item.Value))
                continue;

            _popup.PopupEntity(Loc.GetString("horror-tentacle-disarm", ("target", Identity.Entity(hit, EntityManager)), ("item", item.Value)), hit, PopupType.MediumCaution);
        }
    }
}

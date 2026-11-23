using System.Linq;
using Content.Shared._Polonium.Abductors;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Administration.Logs;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorChamberSystem : EntitySystem
{
    private static readonly ProtoId<AbductorCuratorLinePrototype> DeadLine = "AbductorCuratorDead";
    private static readonly ProtoId<AbductorCuratorLinePrototype> NoGlandLine = "AbductorCuratorNoGland";
    private static readonly ProtoId<AbductorCuratorLinePrototype> RepeatLine = "AbductorCuratorRepeat";
    private static readonly ProtoId<AbductorCuratorLinePrototype> NoMindLine = "AbductorCuratorNoMind";

    [Dependency] private AbducteeSystem _abductee = default!;
    [Dependency] private AbductorBatonSystem _baton = default!;
    [Dependency] private AbductorGlandSystem _glands = default!;
    [Dependency] private AbductorOrderSystem _orders = default!;
    [Dependency] private AbductorShipSystem _ship = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSurgerySystem _surgery = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AbductorChamberComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<AbductorChamberComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<AbductorChamberComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<AbductorChamberComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<AbductorChamberComponent, AbductorExperimentDoAfterEvent>(OnExperiment);
        SubscribeLocalEvent<AbductorChamberComponent, ContainerRelayMovementEntityEvent>(OnRelayMovement);
    }

    private void OnInsertAttempt(Entity<AbductorChamberComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == ent.Comp.ContainerId
            && (!HasComp<MobStateComponent>(args.EntityUid) || HasComp<AbductorComponent>(args.EntityUid)))
        {
            args.Cancel();
        }
    }

    private void OnInserted(Entity<AbductorChamberComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ContainerId)
            _appearance.SetData(ent, AbductorChamberVisuals.Occupied, true);
    }

    private void OnRemoved(Entity<AbductorChamberComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ContainerId)
            _appearance.SetData(ent, AbductorChamberVisuals.Occupied, false);
    }

    private void OnRelayMovement(Entity<AbductorChamberComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        if (GetSubject(ent) == args.Entity && !_baton.IsSubdued(args.Entity))
            _container.TryRemoveFromContainer(args.Entity);
    }

    private void OnActivate(Entity<AbductorChamberComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        if (!HasComp<AbductorComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("abductor-unfamiliar"), ent, args.User);
            return;
        }

        if (GetSubject(ent) == null)
        {
            _popup.PopupEntity(Loc.GetString("abductor-chamber-empty"), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.ExperimentDuration, new AbductorExperimentDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _audio.PlayPvs(ent.Comp.StartSound, ent);
    }

    private void OnExperiment(Entity<AbductorChamberComponent> ent, ref AbductorExperimentDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (!_ship.TryGetShip(args.User, out var ship))
            return;

        if (GetSubject(ent) is not { } subject)
        {
            _popup.PopupEntity(Loc.GetString("abductor-chamber-empty"), ent, args.User);
            return;
        }

        if (_mobState.IsDead(subject))
        {
            Refuse(ent, ship, subject, DeadLine);
            return;
        }

        if (_ship.IsCatalogued(ship, subject))
        {
            Refuse(ent, ship, subject, RepeatLine);
            return;
        }

        if (!HasGland(subject))
        {
            Refuse(ent, ship, subject, NoGlandLine);
            return;
        }

        if (!HasComp<ActorComponent>(subject))
        {
            _audio.PlayPvs(ent.Comp.FailureSound, ent);
            _ship.Say(ship, NoMindLine);
            Release(ent, ship, subject);
            return;
        }

        var clean = IsClean(subject);
        var orderDone = _orders.Settle(subject);
        var credits = ent.Comp.Credits
            + (clean ? ent.Comp.CleanWorkBonus : 0)
            + (orderDone == true ? ent.Comp.OrderBonus : 0);

        _audio.PlayPvs(ent.Comp.SuccessSound, ent);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):user} finished an abductor experiment on {ToPrettyString(subject):target}, clean: {clean}, order: {orderDone}");
        _ship.RecordExperiment(ship, subject, clean, credits, _glands.WakeAll(subject), orderDone);
        _abductee.Assign(subject);
        Release(ent, ship, subject);
    }

    private void Refuse(Entity<AbductorChamberComponent> ent, Entity<AbductorShipComponent> ship, EntityUid subject, ProtoId<AbductorCuratorLinePrototype> line)
    {
        _audio.PlayPvs(ent.Comp.FailureSound, ent);
        _ship.Say(ship, line);
        _container.TryRemoveFromContainer(subject);
    }

    private void Release(Entity<AbductorChamberComponent> ent, Entity<AbductorShipComponent> ship, EntityUid subject)
    {
        _container.TryRemoveFromContainer(subject);

        if (TryComp<CuffableComponent>(subject, out var cuffable))
        {
            foreach (var cuffs in cuffable.Container.ContainedEntities)
            {
                QueueDel(cuffs);
            }
        }

        if (_ship.GetReturnPoint(ship, subject) is { } destination)
            _ship.Teleport(subject, destination);

        _status.TryUpdateStatusEffectDuration(subject, SleepingSystem.StatusEffectForcedSleeping, ent.Comp.ReturnSleep);
        _popup.PopupEntity(Loc.GetString("abductor-chamber-returned"), subject, subject, PopupType.MediumCaution);
    }

    private EntityUid? GetSubject(Entity<AbductorChamberComponent> ent)
    {
        return _container.TryGetContainer(ent, ent.Comp.ContainerId, out var container) && container.ContainedEntities.Count > 0
            ? container.ContainedEntities[0]
            : null;
    }

    private bool HasGland(EntityUid subject)
    {
        return _body.EnumerateOrgans<AbductorGlandComponent>(subject).Any();
    }

    private bool IsClean(EntityUid subject)
    {
        if (TryComp<BloodstreamComponent>(subject, out var bloodstream) && bloodstream.BleedAmount > 0f)
            return false;

        if (!TryComp<BodyComponent>(subject, out var body) || body.Organs == null)
            return true;

        return !body.Organs.ContainedEntities.Any(_surgery.HasUnfinishedSurgerySteps);
    }
}

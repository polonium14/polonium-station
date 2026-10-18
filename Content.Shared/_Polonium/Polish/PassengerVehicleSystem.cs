using System.Linq;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Polish;

/// <summary>
/// Pasażerowie pojazdu: limit miejsc, wysiadanie pasażerów, przejęcie kierownicy po wyjściu kierowcy i dźwięk silnika.
/// </summary>
public sealed partial class PassengerVehicleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PassengerVehicleComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<PassengerVehicleComponent, ContainerRelayMovementEntityEvent>(OnRelayMovement);
        SubscribeLocalEvent<PassengerVehicleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<PassengerVehicleComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<PassengerVehicleComponent, MoveEvent>(OnMove);
    }

    private void OnInsertAttempt(Entity<PassengerVehicleComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        if (args.Container.ContainedEntities.Count >= ent.Comp.MaxOccupants)
            args.Cancel();
    }

    private void OnRelayMovement(Entity<PassengerVehicleComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        // Kierowca prowadzi, pasażer próbujący iść po prostu wysiada.
        if (_vehicle.GetOperatorOrNull(ent.Owner) == args.Entity)
            return;

        Exit(ent, args.Entity);
    }

    private void OnGetVerbs(Entity<PassengerVehicleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!IsPassenger(ent, args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("passenger-vehicle-verb-exit"),
            Priority = 1,
            Act = () => Exit(ent, user),
        });
    }

    private void OnOperatorSet(Entity<PassengerVehicleComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.NewOperator != null
            || !TryComp<VehicleComponent>(ent, out var vehicle)
            || !_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            return;

        // Po wyjściu kierowcy za kierownicę siada pierwszy pasażer.
        foreach (var passenger in container.ContainedEntities.ToArray())
        {
            if (passenger == args.OldOperator)
                continue;

            if (_vehicle.TrySetOperator((ent, vehicle), passenger))
                break;
        }
    }

    private void OnMove(Entity<PassengerVehicleComponent> ent, ref MoveEvent args)
    {
        if (ent.Comp.EngineSound == null
            || !_net.IsServer
            || _timing.CurTime < ent.Comp.NextEngineSound
            || !_vehicle.HasOperator(ent.Owner))
            return;

        ent.Comp.NextEngineSound = _timing.CurTime + ent.Comp.EngineSoundLength;
        _audio.PlayPvs(ent.Comp.EngineSound, ent);
    }

    private bool IsPassenger(Entity<PassengerVehicleComponent> ent, EntityUid user)
    {
        return _container.TryGetContainer(ent, ent.Comp.ContainerId, out var container)
               && container.Contains(user)
               && _vehicle.GetOperatorOrNull(ent.Owner) != user;
    }

    private void Exit(Entity<PassengerVehicleComponent> ent, EntityUid passenger)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container)
            || !_container.Remove(passenger, container))
            return;

        _popup.PopupEntity(Loc.GetString("passenger-vehicle-exit", ("vehicle", ent.Owner)), passenger, passenger);
    }
}

using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Vehicles;
using Robust.Shared.Containers;

namespace Content.Server.Vehicles;

public sealed partial class TankPilotSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        var player = args.Target;
        if (!TryComp(player, out RelayInputMoverComponent? relay))
            return;

        var tank = relay.RelayEntity;
        if (!tank.IsValid() || !HasComp<TankTurretComponent>(tank))
            return;

        if (TryComp(tank, out ContainerManagerComponent? manager))
        {
            foreach (var container in manager.Containers.Values)
            {
                if (container.Contains(player))
                {
                    _container.Remove(player, container, force: true);
                    break;
                }
            }
        }

        RemComp<RelayInputMoverComponent>(player);
    }
}
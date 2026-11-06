using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceLinking.Payloads;

namespace Content.Shared._Polonium.DeviceLinking;

/// <summary>
/// Temporary fix for Wizden #44990: a signal with a logic state only raises <see cref="SignalReceivedEvent{T}"/>,
/// so devices that only handle <see cref="SignalReceivedEvent"/> ignore air alarms, logic gates and other logic sources.
/// A high signal reaches those devices as a plain pulse, like before the refactor. Remove once Wizden fixes it.
/// </summary>
public abstract partial class SharedLogicSignalPulseSystem : EntitySystem
{
    [Dependency] private EntityQuery<DoorSignalControlComponent> _doorSignalQuery;
    [Dependency] private EntityQuery<LogicGateComponent> _logicGateQuery;
    [Dependency] private EntityQuery<MemoryCellComponent> _memoryCellQuery;
    [Dependency] private EntityQuery<EdgeDetectorComponent> _edgeDetectorQuery;
    [Dependency] private EntityQuery<NuclearReactorComponent> _reactorQuery;
    [Dependency] private EntityQuery<TurbineComponent> _turbineQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeviceLinkSinkComponent, SignalReceivedEvent<LogicStatePayload>>(OnLogicSignalReceived);
    }

    private void OnLogicSignalReceived(Entity<DeviceLinkSinkComponent> ent, ref SignalReceivedEvent<LogicStatePayload> args)
    {
        if (args.Data.State == SignalState.Low || HandlesLogicState(ent))
            return;

        var pulse = new SignalReceivedEvent(args.Port, args.Trigger);
        RaiseLocalEvent(ent, ref pulse);
    }

    /// <summary>
    /// Whether a system already reacts to <see cref="SignalReceivedEvent{T}"/> on this sink, so a pulse would trigger it twice.
    /// A component whose system gets its own logic state handler has to be added here.
    /// </summary>
    protected virtual bool HandlesLogicState(EntityUid uid)
    {
        return _doorSignalQuery.HasComp(uid)
               || _logicGateQuery.HasComp(uid)
               || _memoryCellQuery.HasComp(uid)
               || _edgeDetectorQuery.HasComp(uid)
               || _reactorQuery.HasComp(uid)
               || _turbineQuery.HasComp(uid);
    }
}

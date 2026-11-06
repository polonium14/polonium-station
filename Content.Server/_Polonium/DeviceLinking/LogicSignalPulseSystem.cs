using Content.Server.Atmos.Piping.Binary.Components;
using Content.Shared._Polonium.DeviceLinking;

namespace Content.Server._Polonium.DeviceLinking;

/// <inheritdoc/>
public sealed partial class LogicSignalPulseSystem : SharedLogicSignalPulseSystem
{
    [Dependency] private EntityQuery<SignalControlledValveComponent> _valveQuery;

    protected override bool HandlesLogicState(EntityUid uid)
    {
        return base.HandlesLogicState(uid) || _valveQuery.HasComp(uid);
    }
}

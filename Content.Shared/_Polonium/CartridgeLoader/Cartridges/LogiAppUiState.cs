using Content.Shared.CartridgeLoader;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class LogiAppUiState(List<LogiAppParcelEntry> parcels) : BoundUserInterfaceState
{
    public readonly List<LogiAppParcelEntry> Parcels = parcels;
}

/// <summary>
/// A single parcel as shown in the LogiApp program.
/// </summary>
/// <param name="Prototype">The parcel's prototype, used to show whether it's a letter or a package.</param>
/// <param name="ArrivalTime">Round time at which the parcel was created.</param>
/// <param name="Status">The parcel's status at the time the state was built.</param>
[Serializable, NetSerializable]
public readonly record struct LogiAppParcelEntry(EntProtoId? Prototype, TimeSpan ArrivalTime, LogiAppParcelStatus Status);

[Serializable, NetSerializable]
public enum LogiAppParcelStatus : byte
{
    WaitingInCargo,
    WithCourier,
    PriorityExpired,
    Unlocked,
    Damaged,
    Opened,
    Destroyed,
}

[Serializable, NetSerializable]
public sealed class LogiAppUiMessageEvent(LogiAppUiAction action) : CartridgeMessageEvent
{
    public readonly LogiAppUiAction Action = action;
}

[Serializable, NetSerializable]
public enum LogiAppUiAction : byte
{
    ClearFinished,
}

using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.CartridgeLoader.Cartridges;

/// <summary>
/// PDA program that notifies its owner about parcels addressed to them and tracks their status.
/// </summary>
[RegisterComponent, Access(typeof(LogiAppCartridgeSystem))]
public sealed partial class LogiAppCartridgeComponent : Component
{
    /// <summary>
    /// Parcels addressed to the PDA owner since the program was installed.
    /// </summary>
    [DataField]
    public List<LogiAppTrackedParcel> Parcels = new();

    /// <summary>
    /// Distance in tiles from the parcel's teleporter beyond which the parcel counts as being with a courier.
    /// </summary>
    [DataField]
    public float CourierDistance = 5f;

    /// <summary>
    /// Body of the notification sent when a new parcel addressed to the PDA owner is created.
    /// </summary>
    [DataField]
    public LocId NewParcelNotification = "logiapp-notification-new-parcel";
}

[DataDefinition]
public sealed partial class LogiAppTrackedParcel
{
    /// <summary>
    /// The parcel entity. May no longer exist, which means the parcel was destroyed.
    /// </summary>
    [DataField]
    public EntityUid Parcel;

    /// <summary>
    /// The delivery teleporter that created the parcel.
    /// </summary>
    [DataField]
    public EntityUid Spawner;

    /// <summary>
    /// The parcel's prototype, kept so the type is still known after the parcel is destroyed.
    /// </summary>
    [DataField]
    public EntProtoId? Prototype;

    /// <summary>
    /// Round time at which the parcel was created.
    /// </summary>
    [DataField]
    public TimeSpan ArrivalTime;
}

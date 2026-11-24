using Content.Server._Polonium.Delivery;
using Content.Shared._Polonium.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.Delivery;
using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.CartridgeLoader.Cartridges;

/// <summary>
/// Handles the LogiApp PDA program: installs it on every PDA, notifies PDA owners about new parcels
/// and reports the status of parcels addressed to them.
/// </summary>
public sealed partial class LogiAppCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId LogiAppCartridge = "LogiAppCartridge";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PdaComponent, MapInitEvent>(OnPdaMapInit);
        SubscribeLocalEvent<DeliveryComponent, DeliverySpawnedEvent>(OnDeliverySpawned);
        SubscribeLocalEvent<LogiAppCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<LogiAppCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
    }

    /// <summary>
    /// Many PDA prototypes override the preinstalled program list, so the program is added here
    /// instead of to each of those lists.
    /// </summary>
    private void OnPdaMapInit(Entity<PdaComponent> ent, ref MapInitEvent args)
    {
        if (!HasComp<CartridgeLoaderComponent>(ent) || _cartridgeLoader.HasProgram<LogiAppCartridgeComponent>(ent.Owner))
            return;

        // Spawned directly into the container like the prototype's preinstalled programs, so it doesn't take disk space checks into account.
        TrySpawnInContainer(LogiAppCartridge, ent, CartridgeLoaderComponent.UnremovableContainerId, out _);
    }

    private void OnDeliverySpawned(Entity<DeliveryComponent> ent, ref DeliverySpawnedEvent args)
    {
        if (ent.Comp.RecipientName is not { } recipient)
            return;

        var arrivalTime = _gameTicker.RoundDuration();
        var prototype = MetaData(ent).EntityPrototype?.ID;

        var query = EntityQueryEnumerator<LogiAppCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out var app, out var cartridge))
        {
            if (cartridge.LoaderUid is not { } loader
                || !TryComp<PdaComponent>(loader, out var pda)
                || pda.OwnerName != recipient)
                continue;

            app.Parcels.Add(new LogiAppTrackedParcel
            {
                Parcel = ent,
                Spawner = args.Spawner,
                Prototype = prototype,
                ArrivalTime = arrivalTime,
            });

            _cartridgeLoader.SendNotification(loader,
                Loc.GetString(cartridge.ProgramName),
                Loc.GetString(app.NewParcelNotification));

            if (TryComp<CartridgeLoaderComponent>(loader, out var loaderComp) && loaderComp.ActiveProgram == uid)
                UpdateUiState((uid, app), loader);
        }
    }

    private void OnUiReady(Entity<LogiAppCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUiState(ent, args.Loader);
    }

    private void OnUiMessage(EntityUid uid, LogiAppCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not LogiAppUiMessageEvent message)
            return;

        if (message.Action == LogiAppUiAction.ClearFinished)
            component.Parcels.RemoveAll(IsFinished);

        UpdateUiState((uid, component), GetEntity(args.LoaderUid));
    }

    /// <summary>
    /// Refreshes the open program when a parcel's status changes. Statuses are polled instead of
    /// following events, because some changes raise none: the delivery penalty sets WasPenalized
    /// without an event, DeliveryOpenedEvent is raised before IsOpened is set, and a carried parcel
    /// gets no MoveEvent when its carrier walks away from the teleporter.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<LogiAppCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out var app, out var cartridge))
        {
            if (curTime < app.NextUpdate)
                continue;

            app.NextUpdate = curTime + app.UpdateInterval;

            if (cartridge.LoaderUid is not { } loader
                || !TryComp<CartridgeLoaderComponent>(loader, out var loaderComp)
                || loaderComp.ActiveProgram != uid
                || !_ui.IsUiOpen(loader, loaderComp.UiKey)
                || !HasStatusChanged(app))
                continue;

            UpdateUiState((uid, app), loader);
        }
    }

    private bool HasStatusChanged(LogiAppCartridgeComponent app)
    {
        foreach (var parcel in app.Parcels)
        {
            if (GetStatus(parcel, app.CourierDistance) != parcel.LastSentStatus)
                return true;
        }

        return false;
    }

    private void UpdateUiState(Entity<LogiAppCartridgeComponent> ent, EntityUid loader)
    {
        var entries = new List<LogiAppParcelEntry>(ent.Comp.Parcels.Count);
        foreach (var parcel in ent.Comp.Parcels)
        {
            var status = GetStatus(parcel, ent.Comp.CourierDistance);
            parcel.LastSentStatus = status;
            entries.Add(new LogiAppParcelEntry(parcel.Prototype, parcel.ArrivalTime, status));
        }

        _cartridgeLoader.UpdateCartridgeUiState(loader, new LogiAppUiState(entries));
    }

    private bool IsFinished(LogiAppTrackedParcel parcel)
    {
        return !TryComp<DeliveryComponent>(parcel.Parcel, out var delivery)
               || TerminatingOrDeleted(parcel.Parcel)
               || delivery.IsOpened;
    }

    /// <summary>
    /// Works out the parcel's current status from the state of the parcel itself.
    /// </summary>
    /// <param name="parcel">The tracked parcel.</param>
    /// <param name="courierDistance">Distance in tiles from the teleporter beyond which the parcel is with a courier.</param>
    public LogiAppParcelStatus GetStatus(LogiAppTrackedParcel parcel, float courierDistance)
    {
        if (!TryComp<DeliveryComponent>(parcel.Parcel, out var delivery) || TerminatingOrDeleted(parcel.Parcel))
            return LogiAppParcelStatus.Destroyed;

        if (delivery.WasPenalized)
            return LogiAppParcelStatus.Damaged;

        if (delivery.IsOpened)
            return LogiAppParcelStatus.Opened;

        if (!delivery.IsLocked)
            return LogiAppParcelStatus.Unlocked;

        if (TryComp<DeliveryPriorityComponent>(parcel.Parcel, out var priority) && priority.Expired)
            return LogiAppParcelStatus.PriorityExpired;

        // Without the teleporter there is no reference point, so the parcel keeps counting as waiting.
        if (TerminatingOrDeleted(parcel.Spawner))
            return LogiAppParcelStatus.WaitingInCargo;

        var parcelPosition = _transform.GetMapCoordinates(parcel.Parcel);
        var spawnerPosition = _transform.GetMapCoordinates(parcel.Spawner);

        return parcelPosition.InRange(spawnerPosition, courierDistance)
            ? LogiAppParcelStatus.WaitingInCargo
            : LogiAppParcelStatus.WithCourier;
    }
}

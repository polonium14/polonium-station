using Content.Server._Polonium.Delivery;
using Content.Shared._Polonium.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.Delivery;
using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.CartridgeLoader.Cartridges;

/// <summary>
/// Handles the LogiApp PDA program: notifies PDA owners about new parcels
/// and reports the status of parcels addressed to them.
/// </summary>
public sealed partial class LogiAppCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeliveryComponent, DeliverySpawnedEvent>(OnDeliverySpawned);
        SubscribeLocalEvent<DeliveryComponent, EntityTerminatingEvent>(OnDeliveryTerminating);
        SubscribeLocalEvent<LogiAppCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<LogiAppCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
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

    /// <summary>
    /// Opened parcels are trash and often get thrown away or recycled. Remember that they were opened,
    /// so deleting them afterwards doesn't show them as destroyed.
    /// </summary>
    private void OnDeliveryTerminating(Entity<DeliveryComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.IsOpened)
            return;

        var query = EntityQueryEnumerator<LogiAppCartridgeComponent>();
        while (query.MoveNext(out var app))
        {
            foreach (var parcel in app.Parcels)
            {
                if (parcel.Parcel == ent.Owner)
                    parcel.WasOpened = true;
            }
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
        return parcel.WasOpened
               || !TryComp<DeliveryComponent>(parcel.Parcel, out var delivery)
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
        if (parcel.WasOpened)
            return LogiAppParcelStatus.Opened;

        if (!TryComp<DeliveryComponent>(parcel.Parcel, out var delivery) || TerminatingOrDeleted(parcel.Parcel))
            return LogiAppParcelStatus.Destroyed;

        // Checked before damage: cutting a parcel open penalizes it too, and an opened parcel is gone either way.
        if (delivery.IsOpened)
            return LogiAppParcelStatus.Opened;

        if (delivery.WasPenalized)
            return LogiAppParcelStatus.Damaged;

        // Breaking a fragile parcel doesn't penalize it, so it's checked on its own.
        if (TryComp<DeliveryFragileComponent>(parcel.Parcel, out var fragile) && fragile.Broken)
            return LogiAppParcelStatus.Damaged;

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

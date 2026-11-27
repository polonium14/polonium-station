using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.CartridgeLoader.Cartridges;
using Content.Server._Polonium.Delivery;
using Content.Shared._Polonium.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.Delivery;
using Content.Shared.Destructible;
using Content.Shared.PDA;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.CartridgeLoader;

[TestFixture]
[TestOf(typeof(LogiAppCartridgeSystem))]
public sealed class LogiAppCartridgeTest : GameTest
{
    private static readonly EntProtoId Pda = "PassengerPDA";
    private static readonly EntProtoId Teleporter = "CargoMailTeleporter";
    private static readonly EntProtoId Letter = "LetterDelivery";
    private const string Owner = "Jan Kowalski";
    private const string OtherOwner = "Anna Nowak";

    /// <summary>
    /// A parcel lands only in the history of the PDA whose owner it is addressed to,
    /// and its status follows the parcel: distance from the teleporter, opening and deletion.
    /// </summary>
    [Test]
    public async Task TracksParcelForOwner()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        var system = entMan.System<LogiAppCartridgeSystem>();
        var loaderSystem = entMan.System<CartridgeLoaderSystem>();
        var pdaSystem = entMan.System<SharedPdaSystem>();
        var transform = entMan.System<SharedTransformSystem>();

        EntityUid pda = default;
        EntityUid otherPda = default;
        EntityUid parcel = default;

        await server.WaitPost(() =>
        {
            var coords = new MapCoordinates(Vector2.Zero, map.MapId);
            pda = entMan.Spawn(Pda, coords);
            otherPda = entMan.Spawn(Pda, coords);
            pdaSystem.SetOwner(pda, entMan.GetComponent<PdaComponent>(pda), pda, Owner);
            pdaSystem.SetOwner(otherPda, entMan.GetComponent<PdaComponent>(otherPda), otherPda, OtherOwner);

            var teleporter = entMan.Spawn(Teleporter, coords);
            parcel = entMan.Spawn(Letter, coords);

            // The test map has no station records, so the recipient is set by hand.
            entMan.GetComponent<DeliveryComponent>(parcel).RecipientName = Owner;
            var ev = new DeliverySpawnedEvent(teleporter);
            entMan.EventBus.RaiseLocalEvent(parcel, ref ev);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(loaderSystem.HasProgram<LogiAppCartridgeComponent>(pda), "Every PDA gets the program installed.");

            var app = GetApp(entMan, loaderSystem, pda);
            Assert.That(app.Comp.Parcels, Has.Count.EqualTo(1));
            Assert.That(GetApp(entMan, loaderSystem, otherPda).Comp.Parcels, Is.Empty, "Parcels for someone else don't show up.");

            var tracked = app.Comp.Parcels[0];
            Assert.That(tracked.Prototype, Is.EqualTo(Letter));
            Assert.That(system.GetStatus(tracked, app.Comp.CourierDistance), Is.EqualTo(LogiAppParcelStatus.WaitingInCargo));

            transform.SetWorldPosition(parcel, new Vector2(app.Comp.CourierDistance + 1, 0));
            Assert.That(system.GetStatus(tracked, app.Comp.CourierDistance), Is.EqualTo(LogiAppParcelStatus.WithCourier));

            transform.SetWorldPosition(parcel, new Vector2(1, 0));
            Assert.That(system.GetStatus(tracked, app.Comp.CourierDistance), Is.EqualTo(LogiAppParcelStatus.WaitingInCargo),
                "A parcel brought back to the teleporter is waiting again.");

            entMan.GetComponent<DeliveryComponent>(parcel).IsLocked = false;
            Assert.That(system.GetStatus(tracked, app.Comp.CourierDistance), Is.EqualTo(LogiAppParcelStatus.Unlocked));

            entMan.GetComponent<DeliveryComponent>(parcel).IsOpened = true;
            Assert.That(system.GetStatus(tracked, app.Comp.CourierDistance), Is.EqualTo(LogiAppParcelStatus.Opened));

            entMan.DeleteEntity(parcel);
            Assert.That(system.GetStatus(tracked, app.Comp.CourierDistance), Is.EqualTo(LogiAppParcelStatus.Opened),
                "An opened parcel thrown away afterwards still counts as opened.");

            var unopened = SpawnParcel(entMan, tracked.Spawner, new MapCoordinates(Vector2.Zero, map.MapId));
            var unopenedTracked = app.Comp.Parcels.Single(p => p.Parcel == unopened);
            entMan.DeleteEntity(unopened);
            Assert.That(system.GetStatus(unopenedTracked, app.Comp.CourierDistance), Is.EqualTo(LogiAppParcelStatus.Destroyed));
        });
    }

    /// <summary>
    /// A broken fragile parcel counts as damaged even though breaking it doesn't penalize it,
    /// until it is opened.
    /// </summary>
    [Test]
    public async Task BrokenFragileParcelIsDamaged()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        var system = entMan.System<LogiAppCartridgeSystem>();
        var loaderSystem = entMan.System<CartridgeLoaderSystem>();
        var pdaSystem = entMan.System<SharedPdaSystem>();
        var destructible = entMan.System<SharedDestructibleSystem>();

        EntityUid pda = default;
        EntityUid parcel = default;

        await server.WaitPost(() =>
        {
            var coords = new MapCoordinates(Vector2.Zero, map.MapId);
            pda = entMan.Spawn(Pda, coords);
            pdaSystem.SetOwner(pda, entMan.GetComponent<PdaComponent>(pda), pda, Owner);
            var teleporter = entMan.Spawn(Teleporter, coords);

            parcel = SpawnParcel(entMan, teleporter, coords);
            entMan.EnsureComponent<DeliveryFragileComponent>(parcel);
            destructible.BreakEntity(parcel);
        });

        await server.WaitAssertion(() =>
        {
            var app = GetApp(entMan, loaderSystem, pda);
            Assert.That(entMan.GetComponent<DeliveryFragileComponent>(parcel).Broken);
            Assert.That(entMan.GetComponent<DeliveryComponent>(parcel).WasPenalized, Is.False);
            Assert.That(system.GetStatus(app.Comp.Parcels[0], app.Comp.CourierDistance), Is.EqualTo(LogiAppParcelStatus.Damaged));

            // Cutting a parcel open penalizes it and opens it; the player should see it as opened, not as still waiting.
            var delivery = entMan.GetComponent<DeliveryComponent>(parcel);
            delivery.WasPenalized = true;
            delivery.IsOpened = true;
            Assert.That(system.GetStatus(app.Comp.Parcels[0], app.Comp.CourierDistance), Is.EqualTo(LogiAppParcelStatus.Opened),
                "A damaged parcel that was opened counts as opened.");
        });
    }

    /// <summary>
    /// "Clear finished" removes only parcels that were opened or destroyed.
    /// </summary>
    [Test]
    public async Task ClearFinishedKeepsPendingParcels()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        var loaderSystem = entMan.System<CartridgeLoaderSystem>();
        var pdaSystem = entMan.System<SharedPdaSystem>();

        EntityUid pda = default;
        EntityUid pending = default;

        await server.WaitPost(() =>
        {
            var coords = new MapCoordinates(Vector2.Zero, map.MapId);
            pda = entMan.Spawn(Pda, coords);
            pdaSystem.SetOwner(pda, entMan.GetComponent<PdaComponent>(pda), pda, Owner);
            var teleporter = entMan.Spawn(Teleporter, coords);

            pending = SpawnParcel(entMan, teleporter, coords);
            var opened = SpawnParcel(entMan, teleporter, coords);
            var destroyed = SpawnParcel(entMan, teleporter, coords);

            entMan.GetComponent<DeliveryComponent>(opened).IsOpened = true;
            entMan.DeleteEntity(destroyed);

            var app = GetApp(entMan, loaderSystem, pda);
            // Typed as the base class, because that's how CartridgeLoaderSystem relays it to the program.
            CartridgeMessageEvent message = new LogiAppUiMessageEvent(LogiAppUiAction.ClearFinished)
            {
                LoaderUid = entMan.GetNetEntity(pda),
            };
            entMan.EventBus.RaiseLocalEvent(app.Owner, message);
        });

        await server.WaitAssertion(() =>
        {
            var parcels = GetApp(entMan, loaderSystem, pda).Comp.Parcels;
            Assert.That(parcels.Select(p => p.Parcel), Is.EquivalentTo(new[] { pending }));
        });
    }

    private static EntityUid SpawnParcel(IEntityManager entMan, EntityUid teleporter, MapCoordinates coords)
    {
        var parcel = entMan.Spawn(Letter, coords);
        entMan.GetComponent<DeliveryComponent>(parcel).RecipientName = Owner;
        var ev = new DeliverySpawnedEvent(teleporter);
        entMan.EventBus.RaiseLocalEvent(parcel, ref ev);
        return parcel;
    }

    private static Entity<LogiAppCartridgeComponent> GetApp(IEntityManager entMan, CartridgeLoaderSystem loaderSystem, EntityUid pda)
    {
        foreach (var program in loaderSystem.GetPreinstalledPrograms(pda))
        {
            if (entMan.TryGetComponent<LogiAppCartridgeComponent>(program, out var app))
                return (program, app);
        }

        Assert.Fail($"{entMan.ToPrettyString(pda)} has no LogiApp program.");
        return default;
    }
}

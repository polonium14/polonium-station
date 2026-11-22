#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server._Polonium.Photography;
using Content.Server.Fax;
using Content.Shared._Polonium.Photography;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Fax;
using Content.Shared.Fax.Components;
using Content.Shared.Power.EntitySystems;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Photography;

/// <summary>
/// A photograph goes through a fax like a sheet of paper: the picture and its caption reach
/// the printout, a copy lives on its own, and an admin removal reaches every copy.
/// </summary>
[TestFixture]
[TestOf(typeof(PoloniumPhotographySystem))]
public sealed class PhotographFaxTest : GameTest
{
    private const string Subject = "subject";
    private const string Shooter = "shooter";
    private const string Caption = "caption";
    private const string CaptionFont = "ReenieBeanie";
    private const string ReceiverName = "receiver";

    private static readonly EntProtoId PhotoProto = "PoloniumPhotograph";
    private static readonly EntProtoId FaxProto = "FaxMachineBase";
    private static readonly Color CaptionColor = Color.Red;

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task FaxSlotAcceptsPhotographs()
    {
        var map = await Pair.CreateTestMap();
        var fax = await SpawnFax(map);
        var photo = await SpawnPhoto(map);

        await Server.WaitAssertion(() =>
        {
            var slot = SEntMan.GetComponent<FaxMachineComponent>(fax).PaperSlot;
            Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(fax, slot, photo, null));
            Assert.That(slot.Item, Is.EqualTo(photo));
        });
    }

    [Test]
    public async Task SendingPrintsTheSamePictureAndKeepsTheOriginal()
    {
        var map = await Pair.CreateTestMap();
        var sender = await SpawnFax(map);
        var receiver = await SpawnFax(map, ReceiverName);
        var photo = await SpawnPhoto(map);

        await Server.WaitPost(() => SEntMan.System<FaxSystem>().Refresh(sender));
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var system = SEntMan.System<FaxSystem>();
            var fax = SEntMan.GetComponent<FaxMachineComponent>(sender);
            system.SetDestination(sender, fax.KnownFaxes.First(p => p.Value == ReceiverName).Key, fax);
            system.Send(sender, fax, new FaxSendMessage { Actor = sender }, photo);
        });
        await RunSeconds(PrintDelay(receiver));

        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<PoloniumPhotographySystem>();
            var original = SEntMan.GetComponent<PoloniumPhotographComponent>(photo);
            var printed = Photos().Single(p => p != photo);
            var copy = SEntMan.GetComponent<PoloniumPhotographComponent>(printed);

            Assert.Multiple(() =>
            {
                Assert.That(copy.PhotoId, Is.Not.EqualTo(original.PhotoId));
                Assert.That(system.GetStoredPhoto(copy.PhotoId), Is.EqualTo(Blob()));
                Assert.That(system.GetStoredPhoto(original.PhotoId), Is.EqualTo(Blob()));
                Assert.That(copy.Caption, Is.EqualTo(Caption));
                Assert.That(copy.CaptionColor, Is.EqualTo(CaptionColor));
                Assert.That(copy.CaptionFont, Is.EqualTo(CaptionFont));

                var stored = system.GetStoredPhotos().Single(p => p.Id == copy.PhotoId);
                Assert.That(stored.Subject, Is.EqualTo(Subject));
                Assert.That(stored.Shooter, Is.EqualTo(Shooter));
            });
        });
    }

    [Test]
    public async Task QueuedCopySurvivesTheOriginal()
    {
        var map = await Pair.CreateTestMap();
        var fax = await SpawnFax(map);
        var photo = await SpawnPhoto(map);

        await Server.WaitPost(() =>
        {
            SEntMan.System<FaxSystem>().Copy(fax, null, new FaxCopyMessage { Actor = fax }, photo);
            SEntMan.DeleteEntity(photo);
        });
        await RunSeconds(PrintDelay(fax));

        await Server.WaitAssertion(() =>
        {
            var copy = SEntMan.GetComponent<PoloniumPhotographComponent>(Photos().Single());
            Assert.That(SEntMan.System<PoloniumPhotographySystem>().GetStoredPhoto(copy.PhotoId), Is.EqualTo(Blob()));
        });
    }

    [Test]
    public async Task DeletingACopyKeepsTheOriginal()
    {
        var map = await Pair.CreateTestMap();
        var fax = await SpawnFax(map);
        var photo = await SpawnPhoto(map);
        var printed = await CopyAndPrint(fax, photo);

        await Server.WaitPost(() => SEntMan.DeleteEntity(printed));

        await Server.WaitAssertion(() =>
        {
            var original = SEntMan.GetComponent<PoloniumPhotographComponent>(photo);
            Assert.That(SEntMan.System<PoloniumPhotographySystem>().GetStoredPhoto(original.PhotoId), Is.EqualTo(Blob()));
        });
    }

    [Test]
    public async Task AdminRemovalReachesEveryCopy()
    {
        var map = await Pair.CreateTestMap();
        var fax = await SpawnFax(map);
        var photo = await SpawnPhoto(map);
        var printed = await CopyAndPrint(fax, photo);

        int originalId = default, copyId = default;
        await Server.WaitAssertion(() =>
        {
            originalId = SEntMan.GetComponent<PoloniumPhotographComponent>(photo).PhotoId;
            copyId = SEntMan.GetComponent<PoloniumPhotographComponent>(printed).PhotoId;
            Assert.That(SEntMan.System<PoloniumPhotographySystem>().DeleteStoredPhoto(copyId));
        });
        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<PoloniumPhotographySystem>();
            Assert.Multiple(() =>
            {
                Assert.That(Photos(), Is.Empty);
                Assert.That(system.GetStoredPhoto(originalId), Is.Null);
                Assert.That(system.GetStoredPhoto(copyId), Is.Null);
            });
        });
    }

    [Test]
    public async Task AdminRemovalWipesAQueuedCopy()
    {
        var map = await Pair.CreateTestMap();
        var fax = await SpawnFax(map);
        var photo = await SpawnPhoto(map);

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<FaxSystem>().Copy(fax, null, new FaxCopyMessage { Actor = fax }, photo);
            var id = SEntMan.GetComponent<PoloniumPhotographComponent>(photo).PhotoId;
            Assert.That(SEntMan.System<PoloniumPhotographySystem>().DeleteStoredPhoto(id));
        });
        await RunSeconds(PrintDelay(fax));

        await Server.WaitAssertion(() =>
        {
            var copy = SEntMan.GetComponent<PoloniumPhotographComponent>(Photos().Single());
            var data = SEntMan.System<PoloniumPhotographySystem>().GetStoredPhoto(copy.PhotoId);
            Assert.That(data, Is.Not.Null);
            Assert.That(data!.All(b => b == 0), "A removed picture must not come back through the print queue.");
        });
    }

    private async Task<EntityUid> SpawnFax(TestMapData map, string? name = null)
    {
        EntityUid fax = default;
        await Server.WaitPost(() =>
        {
            fax = SEntMan.SpawnAtPosition(FaxProto, map.GridCoords);
            SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(fax, false);
            if (name != null)
                SEntMan.GetComponent<FaxMachineComponent>(fax).FaxName = name;
        });
        await RunTicksSync(5);
        return fax;
    }

    private async Task<EntityUid> SpawnPhoto(TestMapData map)
    {
        EntityUid photo = default;
        await Server.WaitPost(() =>
        {
            photo = SEntMan.SpawnAtPosition(PhotoProto, map.GridCoords);
            var stored = new FaxPhotoPrintedEvent(new FaxedPhoto(Blob(), Subject, Shooter, Caption, CaptionColor, CaptionFont));
            SEntMan.EventBus.RaiseLocalEvent(photo, ref stored);
        });
        return photo;
    }

    private async Task<EntityUid> CopyAndPrint(EntityUid fax, EntityUid photo)
    {
        await Server.WaitPost(() =>
            SEntMan.System<FaxSystem>().Copy(fax, null, new FaxCopyMessage { Actor = fax }, photo));
        await RunSeconds(PrintDelay(fax));

        EntityUid printed = default;
        await Server.WaitPost(() => printed = Photos().Single(p => p != photo));
        return printed;
    }

    private static byte[] Blob()
    {
        var blob = new byte[PhotographyConstants.PhotoByteLength];
        blob[0] = 0xAB;
        blob[^1] = 0xCD;
        return blob;
    }

    private List<EntityUid> Photos()
    {
        var photos = new List<EntityUid>();
        var query = SEntMan.EntityQueryEnumerator<PoloniumPhotographComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var meta))
        {
            if (meta.EntityLifeStage < EntityLifeStage.Terminating)
                photos.Add(uid);
        }

        return photos;
    }

    private float PrintDelay(EntityUid fax)
    {
        return SEntMan.GetComponent<FaxMachineComponent>(fax).PrintingTime + 0.5f;
    }
}

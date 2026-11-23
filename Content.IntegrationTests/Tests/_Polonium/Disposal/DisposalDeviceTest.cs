using System.Linq;
using Content.Client.Popups;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Polonium.Disposal;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Unit;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Polonium.Disposal;

public sealed class DisposalDeviceTest : InteractionTest
{
    private const string DisposalUnit = "DisposalUnit";
    private const string MailingUnit = "MailingUnit";
    private const string Item = "Crowbar";
    private const string Address = "Biblioteka";

    private DisposalUnitComponent Unit => SEntMan.GetComponent<DisposalUnitComponent>(STarget!.Value);

    private async Task OpenUnit(string prototype, Enum key)
    {
        await SpawnTarget(prototype);
        await Server.WaitPost(() => SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(STarget!.Value, false));
        await RunTicks(5);
        await Activate();
        Assert.That(IsUiOpen(key));
    }

    private async Task<EntityUid> PutInside(string prototype)
    {
        var inside = EntityUid.Invalid;
        await Server.WaitPost(() =>
        {
            inside = SEntMan.SpawnAtPosition(prototype, SEntMan.GetCoordinates(TargetCoords));
            Assert.That(SEntMan.System<SharedContainerSystem>().Insert(inside, Unit.Container!));
        });
        await RunTicks(1);
        return inside;
    }

    /// <summary>
    /// A click on the mouth with an item in the hand puts the item in.
    /// </summary>
    [Test]
    public async Task HeldItemGoesIn()
    {
        await OpenUnit(DisposalUnit, DisposalUnitUiKey.Key);
        var held = await PlaceInHands(Item);

        await SendBui(DisposalUnitUiKey.Key, new DisposalDeviceMouthMessage());

        Assert.That(Unit.Container!.ContainedEntities, Does.Contain(ToServer(held)));
        Assert.That(HandSys.GetActiveItem((ToServer(Player), Hands)), Is.Null);
    }

    /// <summary>
    /// A click on the mouth with an empty hand takes out what went in last.
    /// </summary>
    [Test]
    public async Task EmptyHandTakesLast()
    {
        await OpenUnit(DisposalUnit, DisposalUnitUiKey.Key);
        var first = await PutInside(Item);
        var second = await PutInside(Item);

        await SendBui(DisposalUnitUiKey.Key, new DisposalDeviceMouthMessage());

        Assert.That(HandSys.GetActiveItem((ToServer(Player), Hands)), Is.EqualTo(second));
        Assert.That(Unit.Container!.ContainedEntities, Is.EquivalentTo(new[] { first }));
    }

    /// <summary>
    /// Something that is not an item stays inside when the player reaches for it, and the game's popup says what it feels like.
    /// </summary>
    [TestCase("MobCat", "polonium-disposal-small-creature")]
    [TestCase("MobCow", "polonium-disposal-large-creature")]
    public async Task CreatureStaysInside(string creature, string popup)
    {
        await OpenUnit(DisposalUnit, DisposalUnitUiKey.Key);
        var inside = await PutInside(creature);

        await SendBui(DisposalUnitUiKey.Key, new DisposalDeviceMouthMessage());

        Assert.That(Unit.Container!.ContainedEntities, Does.Contain(inside));
        Assert.That(HandSys.GetActiveItem((ToServer(Player), Hands)), Is.Null);

        await AssertPopup(popup);
    }

    private async Task AssertPopup(string popup)
    {
        await RunTicks(5);
        var expected = Client.ResolveDependency<ILocalizationManager>().GetString(popup);
        await Client.WaitAssertion(() =>
        {
            var shown = CEntMan.System<PopupSystem>().CursorLabels.Select(label => label.Text);
            Assert.That(shown, Does.Contain(expected));
        });
    }

    /// <summary>
    /// A dead creature is not seen climbing in and the popup does not say it moves.
    /// </summary>
    [Test]
    public async Task DeadCreatureDoesNotMove()
    {
        await OpenUnit(DisposalUnit, DisposalUnitUiKey.Key);
        var inside = await PutInside("MobCat");
        await Server.WaitPost(() => SEntMan.System<MobStateSystem>().ChangeMobState(inside, MobState.Dead));
        await RunTicks(1);

        await Server.WaitAssertion(() => Assert.That(SEntMan.System<SharedDisposalUnitSystem>().IsSmallCreature(inside), Is.False));
        await SendBui(DisposalUnitUiKey.Key, new DisposalDeviceMouthMessage());

        Assert.That(Unit.Container!.ContainedEntities, Does.Contain(inside));
        await AssertPopup("polonium-disposal-cannot-take");
    }

    /// <summary>
    /// A flush cancelled for lack of an address is not retried at once, so picking an address does not send the mail without the lever.
    /// </summary>
    [Test]
    public async Task CancelledMailFlushWaitsForLever()
    {
        await OpenUnit(MailingUnit, MailingUnitUiKey.Key);
        var letter = await PutInside("Paper");

        await SendBui(MailingUnitUiKey.Key, new DisposalUnitUiButtonPressedMessage(DisposalUnitUiButton.Engage));
        Assert.That(Unit.Engaged);

        var flushTicks = (int) ((Unit.ManualFlushTime.TotalSeconds + 0.5) * STiming.TickRate);
        await RunTicks(flushTicks);
        Assert.That(Unit.Engaged, Is.False);
        Assert.That(Unit.NextFlush, Is.GreaterThan(STiming.CurTime));

        await SendBui(MailingUnitUiKey.Key, new TargetSelectedMessage(Address));
        await RunTicks(5);
        Assert.That(Unit.Container!.ContainedEntities, Does.Contain(letter));
    }

    /// <summary>
    /// Only creatures up to 32 kg that are not a playable species are seen dropping in.
    /// </summary>
    [TestCase("MobMouse", true)]
    [TestCase("MobCat", true)]
    [TestCase("MobMonkey", true)]
    [TestCase("MobKobold", true)]
    [TestCase("MobGoat", false)]
    [TestCase("MobCow", false)]
    [TestCase("MobHuman", false)]
    [TestCase(Item, false)]
    public async Task SmallCreatures(string prototype, bool small)
    {
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnAtPosition(prototype, SEntMan.GetCoordinates(PlayerCoords));
            Assert.That(SEntMan.System<SharedDisposalUnitSystem>().IsSmallCreature(uid), Is.EqualTo(small));
            SEntMan.DeleteEntity(uid);
        });
    }

    /// <summary>
    /// The address board of a mailing unit does nothing without power.
    /// </summary>
    [Test]
    public async Task MailingTargetNeedsPower()
    {
        await OpenUnit(MailingUnit, MailingUnitUiKey.Key);
        var mailing = SEntMan.GetComponent<MailingUnitComponent>(STarget!.Value);

        await SendBui(MailingUnitUiKey.Key, new TargetSelectedMessage(Address));
        Assert.That(mailing.Target, Is.EqualTo(Address));

        await Server.WaitPost(() =>
        {
            var power = SEntMan.System<SharedPowerReceiverSystem>();
            power.SetNeedsPower(STarget!.Value, true);
            power.SetPowerDisabled(STarget!.Value, true);
        });
        await RunTicks(5);

        await SendBui(MailingUnitUiKey.Key, new TargetSelectedMessage(null));
        Assert.That(mailing.Target, Is.EqualTo(Address));
    }
}

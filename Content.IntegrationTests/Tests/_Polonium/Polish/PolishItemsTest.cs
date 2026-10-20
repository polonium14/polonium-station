using System.Collections.Generic;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Polonium.Polish;
using Content.Shared._Polonium.Polish;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Cuffs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Polish;

/// <summary>
/// Polskie przedmioty z NSV13: wieszanie krzyży i godeł, Wojtek, Maluch i krzyż (odpowiednik testu polish_content z NSV13).
/// </summary>
[TestOf(typeof(WallHangingSystem))]
public sealed class PolishItemsTest : InteractionTest
{
    private static readonly string[][] WallDecor =
    [
        ["PolishCrossWooden", "PolishCrossWoodenWall"],
        ["PolishCrossGolden", "PolishCrossGoldenWall"],
        ["PolishEmblemWooden", "PolishEmblemWoodenWall"],
        ["PolishEmblemBlack", "PolishEmblemBlackWall"],
    ];

    [Test]
    public async Task HangAndTakeDownWallDecor()
    {
        foreach (var pair in WallDecor)
        {
            var wall = await SpawnTarget("WallSolid");
            await PlaceInHands(pair[0]);
            await Interact();

            Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null, $"{pair[0]} was not used up when hung");
            var decor = await FindEntity(pair[1]);

            Target = SEntMan.GetNetEntity(decor);
            await Interact();

            var held = HandSys.GetActiveItem((SPlayer, Hands));
            Assert.That(held, Is.Not.Null, $"Taking down {pair[1]} gave nothing");
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(held!.Value).EntityPrototype?.ID, Is.EqualTo(pair[0]));
            Assert.That(SEntMan.Deleted(decor) || SEntMan.IsQueuedForDeletion(decor), $"{pair[1]} stayed on the wall");

            await DeleteHeldEntity();
            await Delete(SEntMan.GetEntity(wall));
        }
    }

    [Test]
    public async Task WojtekDrinksBeer()
    {
        var wojtek = await SpawnTarget("MobWojtek");
        var sWojtek = SEntMan.GetEntity(wojtek);
        var damageable = Server.System<DamageableSystem>();

        await Server.WaitPost(() => damageable.TryChangeDamage(sWojtek, new DamageSpecifier { DamageDict = { ["Blunt"] = 40 } }, true));
        var before = damageable.GetTotalDamage(sWojtek);

        var bottle = await PlaceInHands("DrinkBeerBottleFull");
        await Interact();

        var after = damageable.GetTotalDamage(sWojtek);
        Assert.That(after, Is.EqualTo(before - 20), "Wojtek was not healed by the beer");

        var solutions = Server.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetDrainableSolution(SEntMan.GetEntity(bottle), out _, out var solution));
        Assert.That(solution!.Volume.Int(), Is.Zero, "Wojtek did not drink the whole beer");
    }

    [Test]
    public async Task MaluchSeatsFive()
    {
        var maluch = await Spawn("VehicleMaluch");
        var sMaluch = SEntMan.GetEntity(maluch);
        var vehicles = Server.System<VehicleSystem>();
        var containers = Server.System<SharedContainerSystem>();

        var riders = new List<EntityUid>();
        await Server.WaitPost(() =>
        {
            for (var i = 0; i < 6; i++)
            {
                var rider = SEntMan.SpawnEntity("MobHuman", SEntMan.GetCoordinates(PlayerCoords));
                riders.Add(rider);
                vehicles.TryEnter(sMaluch, rider);
            }
        });
        await RunTicks(1);

        var seats = containers.GetContainer(sMaluch, "maluch_seats");
        Assert.That(seats.ContainedEntities, Has.Count.EqualTo(5), "Maluch should seat exactly five");
        Assert.That(vehicles.GetOperatorOrNull(sMaluch), Is.EqualTo(riders[0]), "The first one in should drive");

        // Kierowca wysiada - za kierownicę siada następny.
        await Server.WaitPost(() => vehicles.TryExit(sMaluch));
        await RunTicks(1);
        Assert.That(vehicles.GetOperatorOrNull(sMaluch), Is.EqualTo(riders[1]), "A passenger should take the wheel");

        await Server.WaitPost(() =>
        {
            foreach (var rider in riders)
            {
                SEntMan.DeleteEntity(rider);
            }
        });
    }

    [Test]
    public async Task CrucifixBlocksPlainBuckling()
    {
        var crucifix = await SpawnTarget("PolishCrucifix");
        var buckle = Server.System<SharedBuckleSystem>();

        EntityUid victim = default;
        var buckled = true;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity("MobHuman", SEntMan.GetCoordinates(TargetCoords));
            buckled = buckle.TryBuckle(victim, victim, SEntMan.GetEntity(crucifix));
        });

        Assert.That(buckled, Is.False, "Anyone could just climb onto the cross");
        await Server.WaitPost(() => SEntMan.DeleteEntity(victim));
    }
}

[TestOf(typeof(CrucifixSystem))]
public sealed class PolishCrucifixTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private async Task<(EntityUid Crucifix, EntityUid Victim)> SetUp(bool cuffed, bool pull)
    {
        var crucifix = await SpawnTarget("PolishCrucifix");
        var cuffs = Server.System<SharedCuffableSystem>();
        var pulling = Server.System<PullingSystem>();

        EntityUid victim = default;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity("MobHuman", SEntMan.GetCoordinates(TargetCoords));
            if (cuffed)
            {
                var cuff = SEntMan.SpawnEntity("Handcuffs", SEntMan.GetCoordinates(TargetCoords));
                Assert.That(cuffs.TryAddNewCuffs(victim, SPlayer, cuff), "The victim could not be cuffed");
            }

            if (pull)
                Assert.That(pulling.TryStartPull(SPlayer, victim), "The victim could not be pulled");
        });
        await RunTicks(5);
        return (SEntMan.GetEntity(crucifix), victim);
    }

    private EntityUid? BuckledTo(EntityUid victim)
    {
        return SEntMan.GetComponent<BuckleComponent>(victim).BuckledTo;
    }

    [Test]
    public async Task DraggedCuffedVictimIsCrucified()
    {
        var (crucifix, victim) = await SetUp(true, false);

        await DragDrop(SEntMan.GetNetEntity(victim), SEntMan.GetNetEntity(crucifix));
        await RunSeconds(14);

        Assert.That(BuckledTo(victim), Is.EqualTo(crucifix), "Dragging the cuffed victim onto the cross did nothing");
        await Server.WaitPost(() => SEntMan.DeleteEntity(victim));
    }

    [Test]
    public async Task PulledCuffedVictimIsCrucifiedWithFreeHand()
    {
        Pair.ClientLogHandler.JudgeLog += (sawmill, log) => sawmill == "system.joint" && log.RenderMessage().Contains("pull-joint");
        var (crucifix, victim) = await SetUp(true, true);

        Assert.That(HandSys.TryGetEmptyHand((SPlayer, Hands), out var emptyHand), "No free hand left");

        await Server.WaitPost(() => HandSys.TrySetActiveHand((SPlayer, Hands), emptyHand));
        await Interact(awaitDoAfters: false);
        await RunSeconds(14);

        Assert.That(BuckledTo(victim), Is.EqualTo(crucifix), "Clicking the cross while pulling the victim did nothing");
        await Server.WaitPost(() => SEntMan.DeleteEntity(victim));
    }

    [Test]
    public async Task FreeVictimIsNotCrucified()
    {
        var (crucifix, victim) = await SetUp(false, false);

        await DragDrop(SEntMan.GetNetEntity(victim), SEntMan.GetNetEntity(crucifix));
        await RunSeconds(14);

        Assert.That(BuckledTo(victim), Is.Null, "A free victim should not be nailed to the cross");
        await Server.WaitPost(() => SEntMan.DeleteEntity(victim));
    }
}

/// <summary>
/// Polski sztandar leczy posiadacza, a jego wersja z craftingu nic nie robi.
/// </summary>
[TestOf(typeof(InspiringBannerSystem))]
public sealed class PolishBannerTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [Test]
    public async Task BannerHealsHolder()
    {
        var damageable = Server.System<DamageableSystem>();
        await Server.WaitPost(() => damageable.TryChangeDamage(SPlayer, new DamageSpecifier { DamageDict = { ["Blunt"] = 20, ["Heat"] = 20 } }, true));
        var before = damageable.GetTotalDamage(SPlayer);

        await PlaceInHands("PolishBanner");
        await UseInHand();
        await RunTicks(1);

        var after = damageable.GetTotalDamage(SPlayer);
        Assert.That(after, Is.EqualTo(before - 30), "The banner should heal 15 brute and 15 burn");

        // Drugie machnięcie od razu nie działa - sztandar ma minutę odnowienia.
        await UseInHand();
        await RunTicks(1);
        Assert.That(damageable.GetTotalDamage(SPlayer), Is.EqualTo(after));
    }

    [Test]
    public async Task CraftedBannerDoesNothing()
    {
        await PlaceInHands("PolishBannerMundane");
        Assert.That(SEntMan.HasComponent<InspiringBannerComponent>(HandSys.GetActiveItem((SPlayer, Hands))), Is.False);
    }
}

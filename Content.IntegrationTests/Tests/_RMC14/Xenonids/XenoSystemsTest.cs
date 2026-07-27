#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.Events;
using Content.Shared._RMC14.Xenonids.Construction.FloorResin;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.ResinSurge;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RMC14.Xenonids;

[TestOf(typeof(XenoComponent))]
public sealed class XenoSystemsTest : GameTest
{
    private static readonly ProtoId<RadioChannelPrototype> Hivemind = "Hivemind";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: XenoEgg
  id: TestGrownXenoEgg
  components:
  - type: XenoEgg
    grown: true
    nextHatch: 9999999
";

    [SidedDependency(Side.Server)] private readonly XenoPlasmaSystem _plasma = null!;

    [Test]
    public async Task SpawnLarvaHasXenoAndHivemind()
    {
        EntityUid larva = default;

        await Pair.Server.WaitPost(() =>
        {
            larva = SSpawn("CMXenoLarva");
        });

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<XenoComponent>(larva), Is.True);
            Assert.That(SComp<ActiveRadioComponent>(larva).Channels, Does.Contain(Hivemind));

            // Contains is Execute access - friend only for XenoEvolutionSystem
#pragma warning disable RA0002
            var evolution = SComp<XenoEvolutionComponent>(larva);
            Assert.That(evolution.EvolvesTo.Contains(new EntProtoId("CMXenoDrone")), Is.True);
#pragma warning restore RA0002
        });
    }

    [Test]
    public async Task PlasmaSpendAndRegen()
    {
        EntityUid drone = default;

        await Pair.Server.WaitPost(() =>
        {
            drone = SSpawn("CMXenoDrone");
            var plasma = SEntity<XenoPlasmaComponent>(drone);
            _plasma.SetPlasma(plasma, 100);

            Assert.That(_plasma.TryRemovePlasma(plasma, 50), Is.True);
            Assert.That(SComp<XenoPlasmaComponent>(drone).Plasma, Is.EqualTo((FixedPoint2)50));

            _plasma.RegenPlasma(plasma, 25);
            Assert.That(SComp<XenoPlasmaComponent>(drone).Plasma, Is.EqualTo((FixedPoint2)75));
        });
    }

    [Test]
    public async Task PlantWeeds()
    {
        var map = await Pair.CreateTestMap();
        var coords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        await Pair.Server.WaitPost(() =>
        {
            var drone = SSpawnAtPosition("CMXenoDrone", coords);
            var plant = new XenoPlantWeedsActionEvent
            {
                PlasmaCost = 75,
            };

            SEntMan.EventBus.RaiseLocalEvent(drone, plant);

            Assert.That(plant.Handled, Is.True);

            var weeds = SEntMan.System<EntityLookupSystem>()
                .GetEntitiesInRange<XenoWeedsComponent>(coords, 1.0f);
            Assert.That(weeds, Is.Not.Empty);
        });
    }

    [Test]
    public async Task SecreteWall()
    {
        var map = await Pair.CreateTestMap();
        var droneCoords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
        var buildCoords = map.GridCoords.Offset(new Vector2(1.5f, 0.5f));

        await Pair.Server.WaitPost(() =>
        {
            var drone = SSpawnAtPosition("CMXenoDrone", droneCoords);
            SSpawnAtPosition("XenoWeeds", buildCoords);

            var netCoords = SEntMan.GetNetCoordinates(buildCoords);
            var ev = new XenoSecreteStructureDoAfterEvent(netCoords, "WallXenoResin");
            var doAfterArgs = new DoAfterArgs(SEntMan, drone, TimeSpan.Zero, ev, drone);
            ev.DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero);

            SEntMan.EventBus.RaiseLocalEvent(drone, ev);

            Assert.That(ev.Handled, Is.True);

            var walls = SEntMan.System<EntityLookupSystem>()
                .GetEntitiesInRange<XenoConstructComponent>(buildCoords, 0.75f);
            Assert.That(
                walls.Any(w => SComp<MetaDataComponent>(w).EntityPrototype?.ID == "WallXenoResin"),
                Is.True);
        });
    }

    [Test]
    public async Task ResinSurgeSticky()
    {
        var map = await Pair.CreateTestMap();
        var droneCoords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
        var targetCoords = map.GridCoords.Offset(new Vector2(2.5f, 0.5f));

        await Pair.Server.WaitPost(() =>
        {
            var drone = SSpawnAtPosition("CMXenoDrone", droneCoords);
            var netCoords = SEntMan.GetNetCoordinates(targetCoords);
            var ev = new ResinSurgeStickyResinDoafter(netCoords, 0);
            var doAfterArgs = new DoAfterArgs(SEntMan, drone, TimeSpan.Zero, ev, drone);
            ev.DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero);

            SEntMan.EventBus.RaiseLocalEvent(drone, ev);

            var sticky = SEntMan.System<EntityLookupSystem>()
                .GetEntitiesInRange<XenoStickyResinComponent>(targetCoords, 2.0f);
            Assert.That(sticky, Is.Not.Empty);
        });
    }

    [Test]
    public async Task AcidCorrode()
    {
        var map = await Pair.CreateTestMap();
        var droneCoords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
        var wallCoords = map.GridCoords.Offset(new Vector2(1.5f, 0.5f));

        await Pair.Server.WaitPost(() =>
        {
            var drone = SSpawnAtPosition("CMXenoDrone", droneCoords);
            var wall = SSpawnAtPosition("WallXenoResin", wallCoords);

            var acidSrc = new XenoCorrosiveAcidEvent
            {
                AcidId = "XenoAcidWeak",
                Strength = XenoAcidStrength.Weak,
                PlasmaCost = 75,
                Time = TimeSpan.FromSeconds(300),
                Dps = 4,
            };
            var ev = new XenoCorrosiveAcidDoAfterEvent(acidSrc);
            var doAfterArgs = new DoAfterArgs(SEntMan, drone, TimeSpan.Zero, ev, drone, target: wall);
            ev.DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero);

            SEntMan.EventBus.RaiseLocalEvent(drone, ev);

            Assert.That(ev.Handled, Is.True);
            Assert.That(SEntMan.HasComponent<DamageableCorrodingComponent>(wall), Is.True);
        });
    }

    [Test]
    public async Task EggHatchesLarva()
    {
        var map = await Pair.CreateTestMap();
        var coords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        EntityUid egg = default;
        EntityUid xeno = default;

        await Pair.Server.WaitPost(() =>
        {
            egg = SSpawnAtPosition("TestGrownXenoEgg", coords);
            xeno = SSpawnAtPosition("CMXenoDrone", coords);

            var activate = new ActivateInWorldEvent(xeno, egg, complex: true);
            SEntMan.EventBus.RaiseLocalEvent(egg, activate);

            Assert.That(activate.Handled, Is.True);
        });

        await Pair.RunTicksSync(5);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(egg), Is.True);

            var larvae = SEntMan.System<EntityLookupSystem>()
                .GetEntitiesInRange<XenoComponent>(coords, 1.5f);
            Assert.That(
                larvae.Any(l => SComp<MetaDataComponent>(l).EntityPrototype?.ID == "CMXenoLarva"),
                Is.True);
        });
    }

    [Test]
    public async Task QueenLayEgg()
    {
        var map = await Pair.CreateTestMap();
        var coords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        await Pair.Server.WaitPost(() =>
        {
            SSpawnAtPosition("XenoWeedsSource", coords);
            var queen = SSpawnAtPosition("CMXenoQueen", coords);

            var ev = new XenoLayEggDoAfterEvent();
            var doAfterArgs = new DoAfterArgs(SEntMan, queen, TimeSpan.Zero, ev, queen);
            ev.DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero);

            SEntMan.EventBus.RaiseLocalEvent(queen, ev);

            Assert.That(ev.Handled, Is.True);

            var eggs = SEntMan.System<EntityLookupSystem>()
                .GetEntitiesInRange<XenoEggComponent>(coords, 1.0f);
            Assert.That(eggs, Is.Not.Empty);
        });
    }

    [Test]
    public async Task LarvaEvolvesToDrone()
    {
        var map = await Pair.CreateTestMap();
        var coords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        EntityUid larva = default;
        var evolved = false;

        await Pair.Server.WaitPost(() =>
        {
            larva = SSpawnAtPosition("CMXenoLarva", coords);

            var mindSys = SEntMan.System<Content.Shared.Mind.SharedMindSystem>();
            var mind = mindSys.CreateMind(null, "xeno-test");
            mindSys.TransferTo(mind.Owner, larva, mind: mind.Comp);

            var evoSys = SEntMan.System<XenoEvolutionSystem>();
            evolved = evoSys.TryForceEvolve(larva, "CMXenoDrone");
            Assert.That(evolved, Is.True);
        });

        await Pair.RunTicksSync(5);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(larva), Is.True);

            var drones = SEntMan.System<EntityLookupSystem>()
                .GetEntitiesInRange<XenoComponent>(coords, 1.5f);
            Assert.That(
                drones.Any(d => SComp<MetaDataComponent>(d).EntityPrototype?.ID == "CMXenoDrone"),
                Is.True);

            var drone = drones.First(d => SComp<MetaDataComponent>(d).EntityPrototype?.ID == "CMXenoDrone");
            Assert.That(SEntMan.TryGetComponent(drone, out XenoPlasmaComponent? plasma), Is.True);
            Assert.That(plasma!.MaxPlasma, Is.GreaterThan(0));

            Assert.That(SEntMan.TryGetComponent(drone, out AlertsComponent? alerts), Is.True);
            Assert.That(
                SEntMan.System<AlertsSystem>().IsShowingAlert(drone, "XenoPlasma"),
                Is.True,
                "Plasma alert should remain after evolution");
        });
    }
}

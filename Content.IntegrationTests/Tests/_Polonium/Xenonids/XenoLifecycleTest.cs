#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Xenonids;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Polonium.Xenonids;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Body;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Polonium.Xenonids;

/// <summary>
/// Cykl z NSV13: jajo, facehugger, embrion, wyklucie larwy i jej wzrost bez Królowej.
/// </summary>
[TestOf(typeof(XenoEmbryoSystem))]
public sealed class XenoLifecycleTest : GameTest
{
    private EntityCoordinates _coords;

    private async Task<EntityCoordinates> Setup()
    {
        var map = await Pair.CreateTestMap();
        _coords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
        return _coords;
    }

    private EntityUid? Mask(EntityUid mob)
    {
        SEntMan.System<InventorySystem>().TryGetSlotEntity(mob, "mask", out var mask);
        return mask;
    }

    [Test]
    public async Task GrownEggHugsAdjacentHuman()
    {
        var coords = await Setup();
        EntityUid egg = default;
        EntityUid human = default;

        await Pair.Server.WaitPost(() =>
        {
            egg = SSpawnAtPosition("XenoEggGrown", coords);
            human = SSpawnAtPosition("MobHuman", coords.Offset(new Vector2(1f, 0f)));
        });

        await RunSeconds(3);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SComp<XenoEggComponent>(egg).State, Is.EqualTo(XenoEggState.Opened));
            var mask = Mask(human);
            Assert.That(mask, Is.Not.Null, "Facehugger nie wskoczył na twarz");
            Assert.That(SEntMan.HasComponent<FacehuggerComponent>(mask), Is.True);
        });
    }

    [Test]
    public async Task HelmetKillsFacehugger()
    {
        var coords = await Setup();
        EntityUid human = default;
        EntityUid hugger = default;
        var leaped = true;

        await Pair.Server.WaitPost(() =>
        {
            human = SSpawnAtPosition("MobHuman", coords);
            var helmet = SSpawnAtPosition("ClothingHeadHelmetEVA", coords);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(human, helmet, "head", force: true), Is.True);

            hugger = SSpawnAtPosition("XenoFacehugger", coords);
            var system = SEntMan.System<FacehuggerSystem>();
            Assert.That(system.CanHug(human), Is.False, "Hełm powinien odstraszać facehuggera");
            leaped = system.TryLeap((hugger, SComp<FacehuggerComponent>(hugger)), human);
        });

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(leaped, Is.False);
            Assert.That(Mask(human), Is.Null);
            Assert.That(SComp<FacehuggerComponent>(hugger).State, Is.EqualTo(FacehuggerState.Dead));
        });
    }

    [Test]
    public async Task FacehuggerImplantsEmbryo()
    {
        var coords = await Setup();
        EntityUid human = default;
        EntityUid hugger = default;

        await Pair.Server.WaitPost(() =>
        {
            human = SSpawnAtPosition("MobHuman", coords);
            hugger = SSpawnAtPosition("XenoFacehugger", coords);
            var system = SEntMan.System<FacehuggerSystem>();
            Assert.That(system.TryLeap((hugger, SComp<FacehuggerComponent>(hugger)), human), Is.True);
        });

        await RunSeconds(17);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SComp<FacehuggerComponent>(hugger).State, Is.EqualTo(FacehuggerState.Impregnated));
            Assert.That(SEntMan.HasComponent<XenoInfectedComponent>(human), Is.True);

            var body = SComp<BodyComponent>(human);
            Assert.That(body.Organs!.ContainedEntities.Any(e => SEntMan.HasComponent<XenoEmbryoComponent>(e)), Is.True);

            // Zarażonego drugi facehugger już nie ruszy.
            Assert.That(SEntMan.System<FacehuggerSystem>().CanHug(human), Is.False);
        });
    }

    [Test]
    public async Task RemovedFacehuggerDoesNotImplant()
    {
        var coords = await Setup();
        EntityUid human = default;

        await Pair.Server.WaitPost(() =>
        {
            human = SSpawnAtPosition("MobHuman", coords);
            var hugger = SSpawnAtPosition("XenoFacehugger", coords);
            SEntMan.System<FacehuggerSystem>().TryLeap((hugger, SComp<FacehuggerComponent>(hugger)), human);
        });

        await RunSeconds(2);

        await Pair.Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<InventorySystem>().TryUnequip(human, "mask", force: true), Is.True);
        });

        await RunSeconds(16);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<XenoInfectedComponent>(human), Is.False);
        });
    }

    [Test]
    public async Task EmbryoBurstsIntoLarvaAndKillsHost()
    {
        var coords = await Setup();
        EntityUid human = default;

        await Pair.Server.WaitPost(() =>
        {
            human = SSpawnAtPosition("MobHuman", coords);
            var embryos = SEntMan.System<XenoEmbryoSystem>();
            Assert.That(embryos.TryInfect(human), Is.True);

            var embryo = SComp<XenoInfectedComponent>(human).Embryo!.Value;
            var comp = SComp<XenoEmbryoComponent>(embryo);
            comp.Stage = comp.MaxStage;
            comp.BurstChance = 1f;
        });

        await RunSeconds(5);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<XenoInfectedComponent>(human), Is.False);
            Assert.That(SEntMan.System<MobStateSystem>().IsDead(human), Is.True, "Nosiciel przeżył wyklucie");

            var larvae = SEntMan.System<EntityLookupSystem>().GetEntitiesInRange<XenoComponent>(coords, 1.5f);
            Assert.That(larvae.Any(l => SComp<MetaDataComponent>(l).EntityPrototype?.ID == "CMXenoLarva"), Is.True);
        });
    }

    [Test]
    public async Task RemovingEmbryoCuresHost()
    {
        var coords = await Setup();
        EntityUid human = default;
        EntityUid embryo = default;

        await Pair.Server.WaitPost(() =>
        {
            human = SSpawnAtPosition("MobHuman", coords);
            Assert.That(SEntMan.System<XenoEmbryoSystem>().TryInfect(human), Is.True);
            embryo = SComp<XenoInfectedComponent>(human).Embryo!.Value;

            // To samo, co robi krok SurgeryStepRemoveOrgan.
            var body = SComp<BodyComponent>(human);
            Assert.That(LimbTargetMap.TryGetOrganByCategory(SEntMan, body, "Torso", out var torso), Is.True);
            var surgeon = SSpawnAtPosition("MobHuman", coords);
            var surgery = SSpawnAtPosition("SurgeryRemoveXenoEmbryo", coords);
            var step = SSpawnAtPosition("SurgeryStepRemoveOrgan", coords);
            var ev = new SurgeryStepEvent(surgeon, human, torso, EntityUid.Invalid, surgery, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
        });

        await RunSeconds(1);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<XenoInfectedComponent>(human), Is.False);
            Assert.That(SComp<BodyComponent>(human).Organs!.ContainedEntities.Contains(embryo), Is.False, "Embrion nadal jest w ciele");
            Assert.That(SEntMan.HasComponent<XenoEmbryoComponent>(embryo), Is.False, "Wyjęty embrion powinien obumrzeć");
        });
    }

    /// <summary>
    /// Pełna operacja przez SurgerySystem: nacięcie, otwarcie klatki piersiowej, potem „Usuń embrion obcego”.
    /// </summary>
    [Test]
    public async Task SurgeryRemovesEmbryo()
    {
        var coords = await Setup();
        EntityUid human = default;
        EntityUid torso = default;
        EntityUid surgeon = default;
        var surgery = SEntMan.System<SurgerySystem>();

        (string Surgery, string Step)[] steps =
        [
            ("SurgeryOpenIncision", "SurgeryStepOpenIncisionScalpel"),
            ("SurgeryOpenIncision", "SurgeryStepRetractSkin"),
            ("SurgeryOpenRibcage", "SurgeryStepSawBones"),
            ("SurgeryOpenRibcage", "SurgeryStepPriseOpenBones"),
            ("SurgeryRemoveXenoEmbryo", "SurgeryStepOpenIncisionScalpel"),
            ("SurgeryRemoveXenoEmbryo", "SurgeryStepClampBleeders"),
            ("SurgeryRemoveXenoEmbryo", "SurgeryStepRemoveOrgan"),
        ];

        await Pair.Server.WaitPost(() =>
        {
            human = SSpawnAtPosition("MobHuman", coords);
            SEntMan.GetComponent<SurgeryTargetComponent>(human).SepsisImmune = true;
            SEntMan.System<StandingStateSystem>().Down(human);
            Assert.That(SEntMan.System<XenoEmbryoSystem>().TryInfect(human), Is.True);
            Assert.That(LimbTargetMap.TryGetOrganByCategory(SEntMan, SComp<BodyComponent>(human), "Torso", out torso), Is.True);

            surgeon = SSpawnAtPosition(null, coords);
            SEntMan.AddComponent<HandsComponent>(surgeon);
            SEntMan.AddComponent<DoAfterComponent>(surgeon);
            SEntMan.AddComponent<ScalpelComponent>(surgeon);
            SEntMan.AddComponent<RetractorComponent>(surgeon);
            SEntMan.AddComponent<BoneSawComponent>(surgeon);
            SEntMan.AddComponent<HemostatComponent>(surgeon);
            SEntMan.AddComponent<TweezersComponent>(surgeon);
            SEntMan.System<SharedHandsSystem>().AddHand(surgeon, "right", HandLocation.Right);
        });

        foreach (var (procedure, step) in steps)
        {
            var started = false;
            await Pair.Server.WaitPost(() =>
            {
                var procedureEnt = surgery.GetSingleton(procedure);
                Assert.That(procedureEnt, Is.Not.Null, $"Brak operacji {procedure}");
                if (surgery.IsStepComplete(human, torso, step, procedureEnt!.Value))
                    return;

                Assert.That(surgery.TryDoSurgeryStep(human, torso, surgeon, procedure, step, out var error), Is.True,
                    $"{procedure}/{step}: {error}");
                started = true;
            });

            if (started)
                await RunSeconds(12);
        }

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<XenoInfectedComponent>(human), Is.False, "Embrion został w środku");
            Assert.That(SComp<BodyComponent>(human).Organs!.ContainedEntities.Any(e => SEntMan.HasComponent<XenoEmbryoComponent>(e)), Is.False);
        });
    }

    [Test]
    public async Task LarvaGrowsWithoutQueen()
    {
        var coords = await Setup();
        EntityUid larva = default;

        await Pair.Server.WaitPost(() =>
        {
            larva = SSpawnAtPosition("CMXenoLarva", coords);
        });

        await RunSeconds(10);

        await Pair.Server.WaitAssertion(() =>
        {
            var points = SComp<XenoEvolutionComponent>(larva).Points;
            Assert.That(points > FixedPoint2.Zero, Is.True, "Larwa bez Królowej nie rośnie");
        });
    }
}

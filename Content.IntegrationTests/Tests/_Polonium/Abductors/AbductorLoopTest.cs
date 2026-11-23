using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Abductors;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Shared._Polonium.Abductors;
using Content.Server._Polonium.Objectives;
using Content.Shared._EinsteinEngines.StepTrigger;
using Content.Shared._Shitmed.Body;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.CCVar;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Movement.Events;
using Content.Shared.Objectives.Systems;
using Content.Shared.Roles.Components;
using Content.Shared.Sprite;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stealth.Components;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Stunnable;
using Content.Shared.Trigger.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;
using NUnit.Framework;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._Polonium.Abductors;

[TestFixture]
[TestOf(typeof(AbductorShipSystem))]
public sealed class AbductorLoopTest : GameTest
{
    private const string MobAbductor = "MobAbductor";
    private const string MobHuman = "MobHuman";
    private const string Telepad = "AbductorAlienPad";
    private const string Chamber = "AbductorExperimentator";
    private const string ObservationConsole = "AbductorHumanObservationConsole";
    private const string Baton = "Wonderprod";
    private const string HeartCategory = "Heart";
    private const string OperatingTable = "AbductorOperatingTable";
    private const string Gland = "OrganAbductorGlandEgg";
    private const string PassiveGland = "OrganAbductorGlandStep";
    private const string FastGland = "TestAbductorGlandFast";
    private const string StealthGland = "OrganAbductorGlandStealth";
    private const string ArcsTrigger = "TestAbductorArcs";
    private const string LightningRod = "TestAbductorLightningRod";
    private const string Wall = "WallSolid";
    private const string StatusConsole = "AbductorConsole";
    private const string Credit = "AbductorCredit";
    private const string LoneObjective = "AbductorExperimentsObjectiveLone";
    private const string RotationRule = "AbductorSpawn";
    private const string ShrinkEffect = "StatusEffectAbductorShrink";
    private const string AlienScalpel = "EnergyScalpelAbductor";
    private const string AlienPinches = "MechanicalPinchesAbductor";
    private const string AlienCautery = "SearingToolAbductor";
    private const string AlienBoneTool = "BoneManipulatorAbductor";

    private static readonly (string Surgery, string Step, string Tool)[] HeartSwap =
    [
        ("SurgeryOpenIncision", "SurgeryStepOpenIncisionScalpel", AlienScalpel),
        ("SurgeryOpenIncision", "SurgeryStepRetractSkin", AlienPinches),
        ("SurgeryOpenRibcage", "SurgeryStepSawBones", AlienScalpel),
        ("SurgeryOpenRibcage", "SurgeryStepPriseOpenBones", AlienPinches),
        ("SurgeryRemoveHeart", "SurgeryStepOpenIncisionScalpel", AlienScalpel),
        ("SurgeryRemoveHeart", "SurgeryStepClampBleeders", AlienPinches),
        ("SurgeryRemoveHeart", "SurgeryStepRemoveOrgan", AlienPinches),
        ("SurgeryInsertHeart", "SurgeryStepSawBones", AlienScalpel),
        ("SurgeryInsertHeart", "SurgeryStepInsertHeart", Gland),
        ("SurgeryInsertHeart", "SurgeryStepSealOrganWound", AlienCautery),
        ("SurgeryCloseIncisionChest", "SurgeryStepCloseBones", AlienPinches),
        ("SurgeryCloseIncisionChest", "SurgeryStepSealRibcage", AlienBoneTool),
        ("SurgeryCloseIncisionChest", "SurgeryStepCloseIncision", AlienCautery),
    ];

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: BaseOrganAbductorGland
  id: TestAbductorGlandFast
  components:
  - type: AbductorGland
    report: AbductorCuratorGlandEgg
    minInterval: 0.1
    maxInterval: 0.2
    uses: 2
  - type: SpawnOnTrigger
    targetUser: true
    proto: FoodEgg

- type: entity
  id: TestAbductorArcs
  components:
  - type: AbductorArcsOnTrigger
    targetUser: true
    bolts: 8

- type: entity
  id: TestAbductorLightningRod
  components:
  - type: Damageable
  - type: Destructible
    thresholds:
    - trigger:
        !type:DamageTrigger
        damage: 1
      behaviors:
      - !type:DoActsBehavior
        acts: [ Destruction ]
  - type: LightningTarget
    lightningExplode: false
    damageFromLightning: 5
";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task MarkedSpecimenIsPulledOntoTheTelepad()
    {
        var (map, ship, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var shipSystem = entMan.System<AbductorShipSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var delay = TimeSpan.FromSeconds(0.5);
        EntityUid telepad = default;

        await server.WaitPost(() => telepad = entMan.SpawnAtPosition(Telepad, map.GridCoords));

        await server.WaitAssertion(() =>
        {
            var marked = new AbductorSpecimenMarkedEvent(specimen);
            entMan.EventBus.RaiseLocalEvent(abductor, ref marked);
            Assert.That(ship.Comp.Marked, Is.EqualTo(specimen));
            Assert.That(shipSystem.TryRetrieve(ship, abductor, delay), Is.False, "A specimen on its feet cannot be pulled up.");

            entMan.System<SharedStunSystem>().TryUpdateParalyzeDuration(specimen, TimeSpan.FromSeconds(30));
            Assert.That(shipSystem.TryRetrieve(ship, abductor, delay), Is.True);
            Assert.That(ship.Comp.Marked, Is.Null);
        });

        await Pair.RunSeconds(1f);

        await server.WaitAssertion(() =>
        {
            var position = transform.GetWorldPosition(specimen);
            Assert.That(Vector2.Distance(position, transform.GetWorldPosition(telepad)), Is.LessThan(0.1f));
            Assert.That(entMan.HasComponent<AbductorSpecimenComponent>(specimen), Is.True);
        });
    }

    [Test]
    public async Task ChamberRefusesSpecimenWithoutGlandAndPaysForCleanWork()
    {
        var (map, ship, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var containers = entMan.System<SharedContainerSystem>();
        var interaction = entMan.System<SharedInteractionSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var releasePoint = map.GridCoords.Offset(new Vector2(1.5f, 0.5f));
        EntityUid chamber = default;
        AbductorChamberComponent chamberComp = default!;

        await server.WaitPost(() =>
        {
            chamber = entMan.SpawnAtPosition(Chamber, map.GridCoords);
            chamberComp = entMan.GetComponent<AbductorChamberComponent>(chamber);
            entMan.System<SharedMapSystem>().SetTile(map.Grid, releasePoint, map.Tile.Tile);
            entMan.System<AbductorShipSystem>().SetReleasePoint(ship, releasePoint);
            entMan.System<SharedMindSystem>().ControlMob(ServerSession!.UserId, specimen);
        });
        await Pair.RunTicksSync(5);

        var duration = (float) chamberComp.ExperimentDuration.TotalSeconds + 0.5f;

        await server.WaitAssertion(() =>
        {
            var container = containers.GetContainer(chamber, chamberComp.ContainerId);
            Assert.That(containers.Insert(specimen, container), Is.True);
            Assert.That(interaction.InteractionActivate(abductor, chamber), Is.True);
        });
        await Pair.RunSeconds(duration);

        await server.WaitAssertion(() =>
        {
            Assert.That(Balance(), Is.Zero, "No gland, no credits.");
            Assert.That(containers.IsEntityInContainer(specimen), Is.False, "A refused specimen is let out.");

            SwapHeartFor(specimen, Gland);

            var container = containers.GetContainer(chamber, chamberComp.ContainerId);
            Assert.That(containers.Insert(specimen, container), Is.True);
            Assert.That(interaction.InteractionActivate(abductor, chamber), Is.True);
        });
        await Pair.RunSeconds(duration);

        await server.WaitAssertion(() =>
        {
            Assert.That(ship.Comp.Experiments, Is.EqualTo(1));
            Assert.That(Balance(), Is.EqualTo(chamberComp.Credits + chamberComp.CleanWorkBonus));
            Assert.That(containers.IsEntityInContainer(specimen), Is.False);
            var position = transform.GetWorldPosition(specimen);
            Assert.That(Vector2.Distance(position, transform.ToWorldPosition(releasePoint)), Is.LessThan(0.1f));
        });
    }

    [Test]
    public async Task BleedingSpecimenEarnsNoBonus()
    {
        var (map, ship, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var containers = entMan.System<SharedContainerSystem>();
        EntityUid chamber = default;
        AbductorChamberComponent chamberComp = default!;

        await server.WaitPost(() =>
        {
            chamber = entMan.SpawnAtPosition(Chamber, map.GridCoords);
            chamberComp = entMan.GetComponent<AbductorChamberComponent>(chamber);
            entMan.System<SharedMindSystem>().ControlMob(ServerSession!.UserId, specimen);

            SwapHeartFor(specimen, Gland);
            entMan.System<BloodstreamSystem>().TryModifyBleedAmount(specimen, 5f);
        });
        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var container = containers.GetContainer(chamber, chamberComp.ContainerId);
            Assert.That(containers.Insert(specimen, container), Is.True);
            Assert.That(entMan.System<SharedInteractionSystem>().InteractionActivate(abductor, chamber), Is.True);
        });
        await Pair.RunSeconds((float) chamberComp.ExperimentDuration.TotalSeconds + 0.5f);

        await server.WaitAssertion(() =>
        {
            Assert.That(ship.Comp.Experiments, Is.EqualTo(1));
            Assert.That(Balance(), Is.EqualTo(chamberComp.Credits));
        });
    }

    [Test]
    public async Task ObservationEndsWhenTheUserLeavesTheConsole()
    {
        var (map, ship, abductor, _) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        EntityUid eye = default;

        await server.WaitAssertion(() =>
        {
            entMan.System<AbductorShipSystem>().SetStationGrid(ship, map.Grid);
            var console = entMan.SpawnAtPosition(ObservationConsole, map.GridCoords);
            Assert.That(entMan.System<SharedInteractionSystem>().InteractionActivate(abductor, console), Is.True);

            var observer = entMan.GetComponent<AbductorObserverComponent>(abductor);
            eye = observer.Eye;
            Assert.That(entMan.GetComponent<EyeComponent>(abductor).Target, Is.EqualTo(eye));
            Assert.That(entMan.GetComponent<AbductorObservationConsoleComponent>(console).ActionEntities, Is.Not.Empty);

            entMan.System<SharedTransformSystem>().SetCoordinates(abductor, map.GridCoords.Offset(new Vector2(20f, 0f)));
        });

        await Pair.RunSeconds(1f);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<AbductorObserverComponent>(abductor), Is.False);
            Assert.That(entMan.Deleted(eye), Is.True);
            Assert.That(entMan.GetComponent<EyeComponent>(abductor).Target, Is.Null);
        });
    }

    [Test]
    public async Task BatonCyclesItsModesOnlyForAbductors()
    {
        var (map, _, abductor, stranger) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var baton = entMan.SpawnAtPosition(Baton, map.GridCoords);
            var comp = entMan.GetComponent<AbductorBatonComponent>(baton);

            entMan.EventBus.RaiseLocalEvent(baton, new UseInHandEvent(stranger));
            Assert.That(comp.Mode, Is.EqualTo(AbductorBatonMode.Stun));

            foreach (var expected in new[] { AbductorBatonMode.Sleep, AbductorBatonMode.Cuff, AbductorBatonMode.Mark, AbductorBatonMode.Stun })
            {
                entMan.EventBus.RaiseLocalEvent(baton, new UseInHandEvent(abductor));
                Assert.That(comp.Mode, Is.EqualTo(expected));
            }
        });
    }

    [Test]
    public async Task AlienToolsSwapTheHeartForAGlandAndEarnTheCleanBonus()
    {
        var (map, ship, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var surgery = entMan.System<SurgerySystem>();
        var hands = entMan.System<SharedHandsSystem>();
        var buckle = entMan.System<SharedBuckleSystem>();
        var containers = entMan.System<SharedContainerSystem>();
        var tools = new Dictionary<string, EntityUid>();
        EntityUid torso = default;
        EntityUid chamber = default;
        AbductorChamberComponent chamberComp = default!;

        await server.WaitAssertion(() =>
        {
            var table = entMan.SpawnAtPosition(OperatingTable, map.GridCoords);
            chamber = entMan.SpawnAtPosition(Chamber, map.GridCoords);
            chamberComp = entMan.GetComponent<AbductorChamberComponent>(chamber);
            entMan.System<SharedTransformSystem>().SetCoordinates(specimen, map.GridCoords);
            entMan.System<SharedMindSystem>().ControlMob(ServerSession!.UserId, specimen);
            Assert.That(buckle.TryBuckle(specimen, abductor, table), Is.True);
            Assert.That(LimbTargetMap.TryGetOrganByCategory(entMan, entMan.GetComponent<BodyComponent>(specimen), "Torso", out torso), Is.True);

            foreach (var (_, _, tool) in HeartSwap)
            {
                if (!tools.ContainsKey(tool))
                    tools[tool] = entMan.SpawnAtPosition(tool, map.GridCoords);
            }
        });
        await Pair.RunTicksSync(5);

        var total = TimeSpan.Zero;
        foreach (var (procedure, step, tool) in HeartSwap)
        {
            var delay = TimeSpan.Zero;
            await server.WaitAssertion(() =>
            {
                var procedureEnt = surgery.GetSingleton(procedure);
                Assert.That(procedureEnt, Is.Not.Null, procedure);
                if (surgery.IsStepComplete(specimen, torso, step, procedureEnt!.Value))
                    return;

                if (hands.GetActiveItem(abductor) is { } held && held != tools[tool])
                    Assert.That(hands.TryDrop(abductor, held, checkActionBlocker: false), Is.True);

                if (hands.GetActiveItem(abductor) == null)
                    Assert.That(hands.TryPickupAnyHand(abductor, tools[tool], checkActionBlocker: false), Is.True, tool);

                Assert.That(surgery.TryDoSurgeryStep(specimen, torso, abductor, procedure, step, out var error), Is.True, $"{procedure}/{step}: {error}");
                delay = entMan.GetComponent<DoAfterComponent>(abductor).DoAfters.Values.Single(doAfter => !doAfter.Completed && !doAfter.Cancelled).Args.Delay;
            });

            if (delay <= TimeSpan.Zero)
                continue;

            total += delay;
            await Pair.RunSeconds((float) delay.TotalSeconds + 0.2f);

            await server.WaitAssertion(() =>
            {
                Assert.That(surgery.IsStepComplete(specimen, torso, step, surgery.GetSingleton(procedure)!.Value), Is.True, $"{procedure}/{step} did not finish.");
            });
        }

        await server.WaitAssertion(() =>
        {
            Assert.That(total.TotalSeconds, Is.InRange(18d, 23d));

            var body = entMan.GetComponent<BodyComponent>(specimen);
            var hearts = body.Organs!.ContainedEntities.Where(organ => entMan.GetComponent<OrganComponent>(organ).Category == HeartCategory).ToArray();
            Assert.That(hearts, Has.Length.EqualTo(1));
            Assert.That(entMan.HasComponent<AbductorGlandComponent>(hearts[0]), Is.True);

            Assert.That(buckle.TryUnbuckle(specimen, abductor), Is.True);
            Assert.That(hands.TryDrop(abductor, checkActionBlocker: false), Is.True);
            Assert.That(containers.Insert(specimen, containers.GetContainer(chamber, chamberComp.ContainerId)), Is.True);
            Assert.That(entMan.System<SharedInteractionSystem>().InteractionActivate(abductor, chamber), Is.True);
        });
        await Pair.RunSeconds((float) chamberComp.ExperimentDuration.TotalSeconds + 0.5f);

        await server.WaitAssertion(() =>
        {
            Assert.That(ship.Comp.Experiments, Is.EqualTo(1));
            Assert.That(Balance(), Is.EqualTo(chamberComp.Credits + chamberComp.CleanWorkBonus));
        });
    }

    [Test]
    public async Task BatonSubduesSleepsCuffsAndMarksASpecimen()
    {
        var (map, ship, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var batonSystem = entMan.System<AbductorBatonSystem>();
        EntityUid baton = default;
        AbductorBatonComponent batonComp = default!;

        void Hit()
        {
            var hit = new MeleeHitEvent(new HashSet<EntityUid> { specimen }, abductor, baton, new DamageSpecifier(), null);
            entMan.EventBus.RaiseLocalEvent(baton, hit);
        }

        await server.WaitAssertion(() =>
        {
            entMan.System<SharedTransformSystem>().SetCoordinates(specimen, map.GridCoords);
            baton = entMan.SpawnAtPosition(Baton, map.GridCoords);
            batonComp = entMan.GetComponent<AbductorBatonComponent>(baton);
            Assert.That(entMan.System<SharedHandsSystem>().TryPickupAnyHand(abductor, baton, checkActionBlocker: false), Is.True);

            entMan.EventBus.RaiseLocalEvent(baton, new UseInHandEvent(abductor));
            Assert.That(batonComp.Mode, Is.EqualTo(AbductorBatonMode.Sleep));
            Hit();
            Assert.That(entMan.HasComponent<SleepingComponent>(specimen), Is.False, "A specimen on its feet cannot be put to sleep.");

            entMan.EventBus.RaiseLocalEvent(baton, new UseInHandEvent(abductor));
            entMan.EventBus.RaiseLocalEvent(baton, new UseInHandEvent(abductor));
            Assert.That(batonComp.Mode, Is.EqualTo(AbductorBatonMode.Mark));
            Hit();
            entMan.EventBus.RaiseLocalEvent(baton, new UseInHandEvent(abductor));
            Assert.That(batonComp.Mode, Is.EqualTo(AbductorBatonMode.Stun));
            Hit();
            Hit();
        });
        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(ship.Comp.Marked, Is.Null, "A specimen on its feet cannot be marked.");
            Assert.That(batonSystem.IsSubdued(specimen), Is.True, "Two hits should bring a specimen down.");

            entMan.EventBus.RaiseLocalEvent(baton, new UseInHandEvent(abductor));
            Assert.That(batonComp.Mode, Is.EqualTo(AbductorBatonMode.Sleep));
            Hit();
        });
        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<SleepingComponent>(specimen), Is.True);
            entMan.EventBus.RaiseLocalEvent(baton, new UseInHandEvent(abductor));
            Assert.That(batonComp.Mode, Is.EqualTo(AbductorBatonMode.Cuff));
            Hit();
        });
        await Pair.RunSeconds((float) batonComp.CuffDelay.TotalSeconds + 0.5f);

        await server.WaitAssertion(() =>
        {
            var cuffable = entMan.GetComponent<CuffableComponent>(specimen);
            Assert.That(entMan.System<SharedCuffableSystem>().IsCuffed((specimen, cuffable)), Is.True);
            entMan.EventBus.RaiseLocalEvent(baton, new UseInHandEvent(abductor));
            Assert.That(batonComp.Mode, Is.EqualTo(AbductorBatonMode.Mark));
            Hit();
        });
        await Pair.RunSeconds((float) batonComp.MarkDelay.TotalSeconds + 0.5f);

        await server.WaitAssertion(() => Assert.That(ship.Comp.Marked, Is.EqualTo(specimen)));
    }

    [Test]
    public async Task OneShotGlandStopsAfterItsUses()
    {
        var (_, _, _, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            SwapHeartFor(specimen, FastGland);
            entMan.System<AbductorGlandSystem>().WakeAll(specimen);
        });
        await Pair.RunSeconds(5f);

        await server.WaitAssertion(() =>
        {
            var eggs = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var meta))
            {
                if (meta.EntityPrototype?.ID == "FoodEgg")
                    eggs++;
            }

            Assert.That(eggs, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task PassiveGlandWorksOnlyWhileInTheBody()
    {
        var (_, _, _, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var containers = entMan.System<SharedContainerSystem>();

        await server.WaitAssertion(() =>
        {
            var gland = SwapHeartFor(specimen, PassiveGland);
            Assert.That(entMan.HasComponent<StepTriggerImmuneComponent>(specimen), Is.False, "A sleeping gland does nothing.");

            entMan.System<AbductorGlandSystem>().WakeAll(specimen);
            Assert.That(entMan.HasComponent<StepTriggerImmuneComponent>(specimen), Is.True);

            Assert.That(containers.Remove(gland, entMan.GetComponent<BodyComponent>(specimen).Organs!), Is.True);
            Assert.That(entMan.HasComponent<StepTriggerImmuneComponent>(specimen), Is.False);
        });
    }

    [Test]
    public async Task ExperimentAdvancesTheObjectiveAndLeavesAnObsession()
    {
        var (map, ship, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var containers = entMan.System<SharedContainerSystem>();
        var minds = entMan.System<SharedMindSystem>();
        var objectives = entMan.System<SharedObjectivesSystem>();
        EntityUid chamber = default;
        AbductorChamberComponent chamberComp = default!;
        EntityUid objective = default;
        Entity<MindComponent> abductorMind = default;

        await server.WaitPost(() =>
        {
            chamber = entMan.SpawnAtPosition(Chamber, map.GridCoords);
            chamberComp = entMan.GetComponent<AbductorChamberComponent>(chamber);
            minds.ControlMob(ServerSession!.UserId, specimen);
            SwapHeartFor(specimen, Gland);

            Assert.That(minds.TryGetMind(abductor, out var mindId, out var mind), Is.True);
            abductorMind = (mindId, mind!);
            Assert.That(objectives.TryCreateObjective(abductorMind, LoneObjective, out var created), Is.True);
            objective = created!.Value;
        });
        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(objectives.GetProgress(objective, abductorMind), Is.Zero);
            Assert.That(containers.Insert(specimen, containers.GetContainer(chamber, chamberComp.ContainerId)), Is.True);
            Assert.That(entMan.System<SharedInteractionSystem>().InteractionActivate(abductor, chamber), Is.True);
        });
        await Pair.RunSeconds((float) chamberComp.ExperimentDuration.TotalSeconds + 0.5f);

        await server.WaitAssertion(() =>
        {
            Assert.That(ship.Comp.Experiments, Is.EqualTo(1));
            Assert.That(objectives.GetProgress(objective, abductorMind), Is.EqualTo(1f / 3f).Within(0.001f));

            var gland = entMan.GetComponent<BodyComponent>(specimen).Organs!.ContainedEntities
                .First(entMan.HasComponent<AbductorGlandComponent>);
            Assert.That(entMan.GetComponent<AbductorGlandComponent>(gland).Awake, Is.True);

            Assert.That(minds.TryGetMind(specimen, out _, out var specimenMind), Is.True);
            Assert.That(specimenMind!.Objectives.Any(entMan.HasComponent<CustomObjectiveConditionComponent>), Is.True);
            Assert.That(specimenMind.MindRoleContainer.ContainedEntities
                .Any(role => entMan.GetComponent<MindRoleComponent>(role).Subtype == "role-subtype-abductee"), Is.True);
        });
    }

    [Test]
    public async Task MothershipOrderPaysItsBonusWhenTheOrganIsReplaced()
    {
        var (map, ship, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var containers = entMan.System<SharedContainerSystem>();
        var prototypes = server.ProtoMan;
        EntityUid chamber = default;
        AbductorChamberComponent chamberComp = default!;

        await server.WaitPost(() =>
        {
            chamber = entMan.SpawnAtPosition(Chamber, map.GridCoords);
            chamberComp = entMan.GetComponent<AbductorChamberComponent>(chamber);
            entMan.System<SharedMindSystem>().ControlMob(ServerSession!.UserId, specimen);
            SwapHeartFor(specimen, Gland);
        });
        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            entMan.System<AbductorOrderSystem>().Issue(ship, specimen);
            var order = prototypes.Index(entMan.GetComponent<AbductorOrderComponent>(specimen).Order);
            Assert.That(order.FromDonor, Is.False, "With one specimen aboard there is no donor to take from.");

            var body = entMan.GetComponent<BodyComponent>(specimen);
            Assert.That(LimbTargetMap.TryGetOrganByCategory(entMan, body, order.Category, out var organ), Is.True);
            var replacement = entMan.SpawnAtPosition(entMan.GetComponent<MetaDataComponent>(organ).EntityPrototype!.ID, map.GridCoords);
            Assert.That(containers.Remove(organ, body.Organs!), Is.True);
            entMan.DeleteEntity(organ);
            Assert.That(containers.Insert(replacement, body.Organs!), Is.True);

            Assert.That(containers.Insert(specimen, containers.GetContainer(chamber, chamberComp.ContainerId)), Is.True);
            Assert.That(entMan.System<SharedInteractionSystem>().InteractionActivate(abductor, chamber), Is.True);
        });
        await Pair.RunSeconds((float) chamberComp.ExperimentDuration.TotalSeconds + 0.5f);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<AbductorOrderComponent>(specimen), Is.False);
            Assert.That(Balance(), Is.EqualTo(chamberComp.Credits + chamberComp.CleanWorkBonus + chamberComp.OrderBonus));
        });
    }

    [Test]
    public async Task ConsoleServesOnlyAbductorsAndTheCowArrivesOnTheTelepad()
    {
        var (map, _, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var telepad = entMan.SpawnAtPosition(Telepad, map.GridCoords);
            var query = entMan.EntityQueryEnumerator<AbductorStatusConsoleComponent>();
            Assert.That(query.MoveNext(out var console, out _), Is.True);

            var denied = new ActivatableUIOpenAttemptEvent(specimen, true);
            entMan.EventBus.RaiseLocalEvent(console, denied);
            Assert.That(denied.Cancelled, Is.True);

            var allowed = new ActivatableUIOpenAttemptEvent(abductor, true);
            entMan.EventBus.RaiseLocalEvent(console, allowed);
            Assert.That(allowed.Cancelled, Is.False);

            entMan.EventBus.RaiseLocalEvent(abductor, new AbductorCowDeliveryEvent());
            var transform = entMan.System<SharedTransformSystem>();
            var padPosition = transform.GetWorldPosition(telepad);
            var cows = 0;
            var metas = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (metas.MoveNext(out _, out var meta, out var xform))
            {
                if (meta.EntityPrototype?.ID == "MobCow" && Vector2.Distance(transform.GetWorldPosition(xform), padPosition) < 0.1f)
                    cows++;
            }

            Assert.That(cows, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CreditsBuyEquipmentFromTheConsole()
    {
        var (_, _, abductor, _) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var query = entMan.EntityQueryEnumerator<AbductorStatusConsoleComponent, StoreComponent>();
            Assert.That(query.MoveNext(out var console, out _, out var store), Is.True);
            Assert.That(store!.FullListingsCatalog.Count(listing => listing.Categories.Contains("AbductorEquipment")), Is.EqualTo(8));

            var hands = entMan.System<SharedHandsSystem>();
            Assert.That(hands.TryDrop(abductor, checkActionBlocker: false) || hands.GetActiveItem(abductor) == null, Is.True);

            entMan.EventBus.RaiseLocalEvent(console, new StoreBuyListingMessage("AbductorStoreHelmet", null) { Actor = abductor });
            Assert.That(hands.GetActiveItem(abductor), Is.Null, "With no credits nothing is bought.");

            entMan.System<SharedStoreSystem>().TryAddCurrency(new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> { [Credit] = 1 }, console, store);
            entMan.EventBus.RaiseLocalEvent(console, new StoreBuyListingMessage("AbductorStoreHelmet", null) { Actor = abductor });
            Assert.That(hands.GetActiveItem(abductor), Is.Not.Null);
            Assert.That(Balance(), Is.Zero);
        });
    }

    [Test]
    public async Task ShrinkRayEffectHalvesTheTargetForItsDuration()
    {
        var (_, _, _, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var scale = entMan.System<SharedScaleVisualsSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.System<StatusEffectsSystem>().TrySetStatusEffectDuration(specimen, ShrinkEffect, TimeSpan.FromSeconds(1)), Is.True);
            Assert.That(scale.GetSpriteScale(specimen), Is.EqualTo(new Vector2(0.5f, 0.5f)));
        });
        await Pair.RunSeconds(2f);

        await server.WaitAssertion(() =>
        {
            Assert.That(scale.GetSpriteScale(specimen), Is.EqualTo(Vector2.One));
            Assert.That(entMan.HasComponent<ScaleVisualsComponent>(specimen), Is.False);
        });
    }

    [Test]
    public async Task EventRotationSkipsTheRuleUntilItsCVarIsOn()
    {
        await Pair.CreateTestMap();
        var server = Pair.Server;
        var events = server.EntMan.System<EventManagerSystem>();
        var config = server.ResolveDependency<IConfigurationManager>();
        var prototype = server.ProtoMan.Index<EntityPrototype>(RotationRule);
        Assert.That(prototype.TryComp<StationEventComponent>(out var stationEvent, server.EntMan.ComponentFactory), Is.True);

        await server.WaitAssertion(() =>
        {
            Assert.That(config.GetCVar(CCVars.AbductorsEnabled), Is.False);
            Assert.That(events.CanRun(prototype, stationEvent!, 100, TimeSpan.FromHours(1)), Is.False);

            config.SetCVar(CCVars.AbductorsEnabled, true);
            Assert.That(events.CanRun(prototype, stationEvent!, 100, TimeSpan.FromHours(1)), Is.True);
            config.SetCVar(CCVars.AbductorsEnabled, false);
        });
    }

    [Test]
    public async Task OnlyAnAwakeSpecimenCanWalkOutOfTheChamber()
    {
        var (map, _, _, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var containers = entMan.System<SharedContainerSystem>();

        await server.WaitAssertion(() =>
        {
            var chamber = entMan.SpawnAtPosition(Chamber, map.GridCoords);
            var container = containers.GetContainer(chamber, entMan.GetComponent<AbductorChamberComponent>(chamber).ContainerId);
            var move = new ContainerRelayMovementEntityEvent(specimen);

            Assert.That(containers.Insert(specimen, container), Is.True);
            entMan.EventBus.RaiseLocalEvent(chamber, ref move);
            Assert.That(containers.IsEntityInContainer(specimen), Is.False, "An awake specimen walks out.");

            entMan.System<SharedStunSystem>().TryUpdateParalyzeDuration(specimen, TimeSpan.FromSeconds(30));
            Assert.That(containers.Insert(specimen, container), Is.True);
            entMan.EventBus.RaiseLocalEvent(chamber, ref move);
            Assert.That(containers.IsEntityInContainer(specimen), Is.True, "A subdued specimen stays inside.");
        });
    }

    [Test]
    public async Task BatonCuffsCanBeTakenOff()
    {
        var (map, _, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var cuffSystem = entMan.System<SharedCuffableSystem>();
            var cuffable = entMan.GetComponent<CuffableComponent>(specimen);
            var baton = entMan.SpawnAtPosition(Baton, map.GridCoords);
            var coordinates = entMan.GetComponent<TransformComponent>(specimen).Coordinates;
            var cuffs = entMan.SpawnAtPosition(entMan.GetComponent<AbductorBatonComponent>(baton).Cuffs, coordinates);

            Assert.That(cuffSystem.TryAddNewCuffs(specimen, abductor, cuffs, cuffable), Is.True);
            Assert.That(cuffSystem.IsCuffed((specimen, cuffable)), Is.True);

            cuffSystem.Uncuff(specimen, abductor, cuffs, cuffable);
            Assert.That(cuffSystem.IsCuffed((specimen, cuffable)), Is.False);
        });
    }

    [Test]
    public async Task GlandArcsDoNotBlowUpWhatTheyHit()
    {
        var (map, _, _, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        EntityUid rod = default;

        await server.WaitAssertion(() =>
        {
            rod = entMan.SpawnAtPosition(LightningRod, map.GridCoords.Offset(new Vector2(2f, 0f)));
            var arcs = entMan.SpawnAtPosition(ArcsTrigger, map.GridCoords);
            Assert.That(entMan.System<TriggerSystem>().Trigger(arcs, specimen), Is.True);
        });
        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() => Assert.That(entMan.Deleted(rod), Is.False));
    }

    [Test]
    public async Task TelepadLeavesMobsInContainersBehind()
    {
        var (map, _, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var containers = entMan.System<SharedContainerSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var landing = map.GridCoords.Offset(new Vector2(1.5f, 0.5f));

        await server.WaitAssertion(() =>
        {
            entMan.System<SharedMapSystem>().SetTile(map.Grid, landing, map.Tile.Tile);
            var telepad = entMan.SpawnAtPosition(Telepad, map.GridCoords);
            var chamber = entMan.SpawnAtPosition(Chamber, map.GridCoords);
            var container = containers.GetContainer(chamber, entMan.GetComponent<AbductorChamberComponent>(chamber).ContainerId);
            Assert.That(containers.Insert(specimen, container), Is.True);

            transform.SetCoordinates(abductor, entMan.GetComponent<TransformComponent>(telepad).Coordinates);
            entMan.System<AbductorShipSystem>().BeginWarp(landing, landing, TimeSpan.FromSeconds(0.1), telepad: telepad);
        });
        await Pair.RunSeconds(0.5f);

        await server.WaitAssertion(() =>
        {
            Assert.That(containers.IsEntityInContainer(specimen), Is.True);
            Assert.That(Vector2.Distance(transform.GetWorldPosition(abductor), transform.ToWorldPosition(landing)), Is.LessThan(0.1f));
        });
    }

    [Test]
    public async Task FailedRetrievalKeepsTheMark()
    {
        var (map, ship, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var containers = entMan.System<SharedContainerSystem>();

        await server.WaitAssertion(() =>
        {
            entMan.SpawnAtPosition(Telepad, map.GridCoords);
            var chamber = entMan.SpawnAtPosition(Chamber, map.GridCoords);
            var marked = new AbductorSpecimenMarkedEvent(specimen);
            entMan.EventBus.RaiseLocalEvent(abductor, ref marked);
            entMan.System<SharedStunSystem>().TryUpdateParalyzeDuration(specimen, TimeSpan.FromSeconds(30));
            Assert.That(entMan.System<AbductorShipSystem>().TryRetrieve(ship, abductor, TimeSpan.FromSeconds(0.1)), Is.True);
            Assert.That(ship.Comp.Marked, Is.Null);

            var container = containers.GetContainer(chamber, entMan.GetComponent<AbductorChamberComponent>(chamber).ContainerId);
            Assert.That(containers.Insert(specimen, container), Is.True);
        });
        await Pair.RunSeconds(0.5f);

        await server.WaitAssertion(() =>
        {
            Assert.That(ship.Comp.Marked, Is.EqualTo(specimen));
            Assert.That(entMan.HasComponent<AbductorSpecimenComponent>(specimen), Is.False);
        });
    }

    [Test]
    public async Task ReturnPointSkipsTilesNobodyCanLandOn()
    {
        var (map, ship, _, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var shipSystem = entMan.System<AbductorShipSystem>();
        var point = map.GridCoords.Offset(new Vector2(1.5f, 0.5f));

        await server.WaitAssertion(() =>
        {
            shipSystem.SetReleasePoint(ship, point);
            Assert.That(shipSystem.GetReturnPoint(ship, specimen), Is.Null, "There is no floor there.");

            entMan.System<SharedMapSystem>().SetTile(map.Grid, point, map.Tile.Tile);
            Assert.That(shipSystem.GetReturnPoint(ship, specimen), Is.EqualTo(point));

            entMan.SpawnAtPosition(Wall, point);
            Assert.That(shipSystem.GetReturnPoint(ship, specimen), Is.Null, "A wall stands there.");
        });
    }

    [Test]
    public async Task CrawlingIsNotBeingSubdued()
    {
        var (_, _, _, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var baton = entMan.System<AbductorBatonSystem>();
            Assert.That(baton.IsSubdued(specimen), Is.False);

            Assert.That(entMan.System<SharedStunSystem>().TryKnockdown(specimen, TimeSpan.FromSeconds(5), true, autoStand: false, drop: false, force: true), Is.True);
            Assert.That(entMan.HasComponent<KnockedDownComponent>(specimen), Is.True);
            Assert.That(baton.IsSubdued(specimen), Is.False);
        });
    }

    [Test]
    public async Task WideSwingSharesItsStaminaDamage()
    {
        var (map, _, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var second = entMan.SpawnAtPosition(MobHuman, map.GridCoords.Offset(new Vector2(1f, 0f)));
            var baton = entMan.SpawnAtPosition(Baton, map.GridCoords);
            var stun = entMan.GetComponent<AbductorBatonComponent>(baton).StunStamina;
            var hit = new MeleeHitEvent(new HashSet<EntityUid> { specimen, second }, abductor, baton, new DamageSpecifier(), null);
            entMan.EventBus.RaiseLocalEvent(baton, hit);

            Assert.That(entMan.GetComponent<StaminaComponent>(specimen).StaminaDamage, Is.EqualTo(stun / 2f).Within(0.01f));
            Assert.That(entMan.GetComponent<StaminaComponent>(second).StaminaDamage, Is.EqualTo(stun / 2f).Within(0.01f));
        });
    }

    [Test]
    public async Task ExperimentCountOutlivesTheShip()
    {
        var (_, ship, abductor, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var entMan = server.EntMan;
        var shipSystem = entMan.System<AbductorShipSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.System<SharedMindSystem>().TryGetMind(abductor, out var mindId, out _), Is.True);
            shipSystem.RecordExperiment(ship, specimen, true, 0, [], null);
            Assert.That(shipSystem.GetExperiments(mindId), Is.EqualTo(1));

            entMan.RemoveComponent<AbductorShipComponent>(ship);
            Assert.That(shipSystem.GetExperiments(mindId), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GlandComponentsAddedAtRuntimeReachTheClient()
    {
        var (map, _, _, specimen) = await SetUpCrew();
        var server = Pair.Server;
        var client = Pair.Client;
        var entMan = server.EntMan;
        EntityUid neighbour = default;

        await server.WaitPost(() =>
        {
            entMan.System<SharedMindSystem>().ControlMob(ServerSession!.UserId, specimen);
            neighbour = entMan.SpawnAtPosition(MobHuman, map.GridCoords.Offset(new Vector2(1f, 1f)));
            SwapHeartFor(specimen, StealthGland);
            SwapHeartFor(neighbour, PassiveGland);
            entMan.System<AbductorGlandSystem>().WakeAll(specimen);
            entMan.System<AbductorGlandSystem>().WakeAll(neighbour);
        });
        await Pair.RunTicksSync(10);

        float minVisibility = default, passiveRate = default, movementRate = default;
        string[] whitelist = default;
        await server.WaitAssertion(() =>
        {
            minVisibility = entMan.GetComponent<StealthComponent>(specimen).MinVisibility;
            var onMove = entMan.GetComponent<StealthOnMoveComponent>(specimen);
            passiveRate = onMove.PassiveVisibilityRate;
            movementRate = onMove.MovementVisibilityRate;
            whitelist = entMan.GetComponent<StepTriggerImmuneComponent>(neighbour).Whitelist.Components;
        });

        var clientSpecimen = Pair.ToClientUid(specimen);
        var clientNeighbour = Pair.ToClientUid(neighbour);
        await client.WaitAssertion(() =>
        {
            Assert.That(client.EntMan.GetComponent<StealthComponent>(clientSpecimen).MinVisibility, Is.EqualTo(minVisibility));
            var onMove = client.EntMan.GetComponent<StealthOnMoveComponent>(clientSpecimen);
            Assert.That(onMove.PassiveVisibilityRate, Is.EqualTo(passiveRate));
            Assert.That(onMove.MovementVisibilityRate, Is.EqualTo(movementRate));
            Assert.That(client.EntMan.GetComponent<StepTriggerImmuneComponent>(clientNeighbour).Whitelist.Components, Is.EquivalentTo(whitelist));
        });
    }

    private EntityUid SwapHeartFor(EntityUid specimen, string gland)
    {
        var entMan = Pair.Server.EntMan;
        var containers = entMan.System<SharedContainerSystem>();
        var organs = entMan.GetComponent<BodyComponent>(specimen).Organs!;
        var heart = organs.ContainedEntities.First(organ => entMan.GetComponent<OrganComponent>(organ).Category == HeartCategory);
        Assert.That(containers.Remove(heart, organs), Is.True);
        entMan.DeleteEntity(heart);

        var replacement = entMan.SpawnAtPosition(gland, entMan.GetComponent<TransformComponent>(specimen).Coordinates);
        Assert.That(containers.Insert(replacement, organs), Is.True);
        return replacement;
    }

    private int Balance()
    {
        var query = Pair.Server.EntMan.EntityQueryEnumerator<AbductorStatusConsoleComponent, StoreComponent>();
        Assert.That(query.MoveNext(out _, out var store), Is.True);
        return store!.Balance.GetValueOrDefault(Credit).Int();
    }

    private async Task<(TestMapData Map, Entity<AbductorShipComponent> Ship, EntityUid Abductor, EntityUid Specimen)> SetUpCrew()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entMan = server.EntMan;
        Entity<AbductorShipComponent> ship = default;
        EntityUid abductor = default;
        EntityUid specimen = default;

        await server.WaitPost(() =>
        {
            ship = (map.Grid.Owner, entMan.EnsureComponent<AbductorShipComponent>(map.Grid.Owner));
            entMan.System<SharedPhysicsSystem>().SetCanCollide(entMan.SpawnAtPosition(StatusConsole, map.GridCoords), false);
            abductor = entMan.SpawnAtPosition(MobAbductor, map.GridCoords);
            var minds = entMan.System<SharedMindSystem>();
            minds.TransferTo(minds.CreateMind(null, "abductor"), abductor);
            var comp = entMan.EnsureComponent<AbductorComponent>(abductor);
            comp.ArrivalLine = "AbductorCuratorArrivalLone";
            entMan.System<AbductorShipSystem>().AddCrew(ship, (abductor, comp));
            specimen = entMan.SpawnAtPosition(MobHuman, map.GridCoords.Offset(new Vector2(0f, 1f)));
        });

        return (map, ship, abductor, specimen);
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Medical.PatientIntake;
using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking;
using Content.Shared.Atmos;
using Content.Server.Storage.EntitySystems;
using Content.Shared._Polonium.Medical.PatientIntake;
using Content.Shared.DeviceLinking;
using Content.Shared.Fax.Components;
using Content.Shared.Gibbing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Paper;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Storage.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

[TestFixture]
[TestOf(typeof(PatientIntakeSystem))]
public sealed class PatientIntakeMachineTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: patientIntakeCase
  id: PatientIntakeTestHealthy
  name: patient-intake-case-easy
  weight: 0
  payMin: 1000
  payMax: 1000
  windowMin: 600
  windowMax: 600
  issues: []

- type: patientIntakeCase
  id: PatientIntakeTestPoisoned
  name: patient-intake-case-easy
  weight: 0
  payMin: 1000
  payMax: 1000
  issues: [ PatientIntakePoisoning ]

- type: patientIntakeCase
  id: PatientIntakeTestTimed
  name: patient-intake-case-timed
  weight: 0
  payMin: 1000
  payMax: 1000
  windowMin: 60
  windowMax: 60
  debitOnTimeout: true
  debitFraction: 0.5
  issues: []

- type: patientIntakeCase
  id: PatientIntakeTestMass
  name: patient-intake-case-mass-casualty
  weight: 0
  payMin: 1000
  payMax: 1000
  patientsMin: 2
  patientsMax: 2
  issues: []
";

    [Test]
    public async Task EveryIssueFailsTheCheck()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var intake = entMan.System<PatientIntakeSystem>();
        var map = await pair.CreateTestMap();

        var issues = server.ProtoMan.EnumeratePrototypes<PatientIntakeIssuePrototype>().ToList();
        var patients = new Dictionary<string, EntityUid>();
        await server.WaitPost(() =>
        {
            AddAir(entMan, map.MapUid);
            foreach (var issue in issues)
                patients[issue.ID] = entMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            foreach (var issue in issues)
            {
                var patient = patients[issue.ID];
                var comp = entMan.AddComponent<PatientIntakePatientComponent>(patient);
                comp.ExpectedParts = PartCategories(entMan, patient);
                Assert.That(intake.ApplyIssue(patient, comp, issue), Is.True, $"{issue.ID} must apply to a human.");
            }
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            foreach (var issue in issues)
            {
                var patient = patients[issue.ID];
                var failures = intake.GetFailures((patient, entMan.GetComponent<PatientIntakePatientComponent>(patient)));
                Assert.That(failures, Is.Not.Empty, $"{issue.ID} left a body that passes the check. expected=[{string.Join(",", entMan.GetComponent<PatientIntakePatientComponent>(patient).ExpectedParts)}] present=[{string.Join(",", PartCategories(entMan, patient))}]");

                if (issue.Kind is PatientIntakeIssueKind.Bone or PatientIntakeIssueKind.Organ)
                {
                    Assert.That(failures.Select(f => f.Kind), Does.Contain(IntakeFailureKind.Trauma),
                        $"{issue.ID} must go through a trauma, or surgery can't treat it.");
                }
            }
        });
    }

    [Test]
    public async Task EveryCaseStartsWithAnUnhealthyPatient()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var intake = entMan.System<PatientIntakeSystem>();
        var cases = server.ProtoMan.EnumeratePrototypes<PatientIntakeCasePrototype>()
            .Where(c => c.Weight > 0)
            .ToList();

        foreach (var caseProto in cases)
        {
            var (machine, patient) = await StartCase(caseProto.ID);
            await server.WaitAssertion(() =>
            {
                var comp = entMan.GetComponent<PatientIntakePatientComponent>(patient);
                Assert.That(comp.Issues, Is.Not.Empty, $"{caseProto.ID} rolled no issues.");
                Assert.That(comp.Gear, Is.Not.Empty, $"{caseProto.ID} patient got no clothes.");
                Assert.That(intake.GetFailures((patient, comp)), Is.Not.Empty, $"{caseProto.ID} patient is already healthy.");
            });
            await server.WaitPost(() => entMan.DeleteEntity(machine));
        }
    }

    [Test]
    public async Task HealthyReturnPaysAndFreesTheMachine()
    {
        var (machine, patient) = await StartCase("PatientIntakeTestHealthy");
        var server = Pair.Server;
        var entMan = server.EntMan;
        var storage = entMan.System<EntityStorageSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<MetaDataComponent>(patient).EntityPaused, Is.True, "Frozen until first opened.");
            Assert.That(entMan.GetComponent<EntityStorageComponent>(machine).Contents.Contains(patient));
        });

        await server.WaitPost(() => storage.OpenStorage(machine));
        await Pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            Assert.That(entMan.GetComponent<MetaDataComponent>(patient).EntityPaused, Is.False);
            Assert.That(comp.Case!.ClockStart, Is.Not.Null, "The clock starts on first open.");
        });

        await server.WaitPost(() => storage.CloseStorage(machine));
        await Pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            Assert.That(comp.Case, Is.Null);
            Assert.That(comp.LastNet, Is.GreaterThan(900), "A healthy, prompt return pays nearly in full.");
            Assert.That(comp.CasesClosed, Is.EqualTo(1));
            Assert.That(comp.TotalNet, Is.EqualTo(comp.LastNet));

            var roundEnd = new RoundEndTextAppendEvent();
            entMan.EventBus.RaiseEvent(EventSource.Local, roundEnd);
            Assert.That(roundEnd.Text, Does.Contain(comp.TotalNet.ToString()), "The round-end summary lists the machine's total.");
            Assert.That(entMan.Deleted(patient) || entMan.IsQueuedForDeletion(patient), Is.True);
        });
    }

    [Test]
    public async Task MassCasualtyPaysWhenTheLastPatientIsBack()
    {
        var (machine, first) = await StartCase("PatientIntakeTestMass", popDelay: TimeSpan.Zero);
        var server = Pair.Server;
        var entMan = server.EntMan;

        await OpenAndClose(machine);
        await Pair.RunTicksSync(5);

        EntityUid second = default;
        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            Assert.That(comp.Case, Is.Not.Null, "One of two patients back: the case stays open.");
            second = comp.Case!.Patients.Single();
            Assert.That(second, Is.Not.EqualTo(first), "The second patient pops once the tray is free.");
        });


        await OpenAndClose(machine);
        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            Assert.That(comp.Case, Is.Null);
            Assert.That(comp.LastNet, Is.GreaterThan(900), "Both shares paid out at the end.");
        });
    }

    [Test]
    public async Task UntreatedReturnPaysNothing()
    {
        var (machine, _) = await StartCase("PatientIntakeTestPoisoned");
        await OpenAndClose(machine);
        await Pair.Server.WaitAssertion(() =>
        {
            var comp = Pair.Server.EntMan.GetComponent<PatientIntakeMachineComponent>(machine);
            Assert.That(comp.Case, Is.Null);
            Assert.That(comp.LastNet, Is.EqualTo(0), "Easy cases never debit for a failed check.");
        });
    }

    [Test]
    public async Task LateTimedReturnDebits()
    {
        var (machine, patient) = await StartCase("PatientIntakeTestTimed");
        var server = Pair.Server;
        var entMan = server.EntMan;
        var storage = entMan.System<EntityStorageSystem>();

        await server.WaitPost(() => storage.OpenStorage(machine));
        await Pair.RunTicksSync(2);
        await server.WaitPost(() =>
        {
            entMan.GetComponent<PatientIntakePatientComponent>(patient).ClockStart =
                server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromMinutes(5);
            storage.CloseStorage(machine);
        });
        await Pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<PatientIntakeMachineComponent>(machine).LastNet, Is.EqualTo(-500)));
    }

    [Test]
    public async Task WrongBodyIsRejected()
    {
        var (machine, _) = await StartCase("PatientIntakeTestHealthy");
        var server = Pair.Server;
        var entMan = server.EntMan;
        var storage = entMan.System<EntityStorageSystem>();

        await server.WaitPost(() => storage.OpenStorage(machine));
        await Pair.RunTicksSync(2);
        await server.WaitPost(() =>
        {
            // Pull the patient away and leave a stranger's corpse on the tray.
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            var patient = comp.Case!.Patients.First();
            var xform = entMan.System<SharedTransformSystem>();
            xform.SetCoordinates(patient, new EntityCoordinates(machine, new Vector2(5, 5)));
            var stranger = entMan.SpawnEntity("MobHuman", new EntityCoordinates(machine, new Vector2(0, -1)));
            entMan.System<Content.Shared.Standing.StandingStateSystem>().Down(stranger);
            storage.TryCloseStorage(machine, machine);
        });
        await Pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<EntityStorageComponent>(machine).Open, Is.True, "The close must be refused.");
            Assert.That(entMan.GetComponent<PatientIntakeMachineComponent>(machine).Case, Is.Not.Null, "Nothing is voided.");
        });
    }

    [Test]
    public async Task BodyHiddenInABagWithThePatientIsRejected()
    {
        var (machine, patient) = await StartCase("PatientIntakeTestHealthy");
        var server = Pair.Server;
        var entMan = server.EntMan;
        var storage = entMan.System<EntityStorageSystem>();

        await server.WaitPost(() => storage.OpenStorage(machine));
        await Pair.RunTicksSync(2);
        await server.WaitPost(() =>
        {
            // Closing would delete everything on the tray, so a body tucked into a bag must stop it.
            var tray = new EntityCoordinates(machine, new Vector2(0, -1));
            var bag = entMan.SpawnEntity("BodyBag", tray);
            var stranger = entMan.SpawnEntity("MobHuman", tray);
            storage.Insert(stranger, bag);
            storage.CloseStorage(bag);
            storage.TryCloseStorage(machine, machine);
        });
        await Pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<EntityStorageComponent>(machine).Open, Is.True, "The close must be refused.");
            Assert.That(entMan.EntityExists(patient), Is.True);
        });
    }

    [Test]
    public async Task FrozenPatientsOrgansAndGearArePausedToo()
    {
        var (_, patient) = await StartCase("PatientIntakeTestHealthy");
        var entMan = Pair.Server.EntMan;
        await Pair.Server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<PatientIntakePatientComponent>(patient);
            var organs = entMan.GetComponent<BodyComponent>(patient).Organs!.ContainedEntities;
            Assert.That(organs, Is.Not.Empty);
            Assert.That(organs.Concat(comp.Gear).All(e => entMan.GetComponent<MetaDataComponent>(e).EntityPaused), Is.True,
                "Organs and worn gear must be frozen with the body, or the patient deteriorates before the tray opens.");
        });
    }

    [Test]
    public async Task MindEnteringThePatientFailsTheCase()
    {
        var (machine, patient) = await StartCase("PatientIntakeTestHealthy");
        var entMan = Pair.Server.EntMan;
        await Pair.Server.WaitPost(() =>
        {
            var minds = entMan.System<SharedMindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, patient);
        });
        await Pair.RunTicksSync(5);
        await Pair.Server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            Assert.That(comp.Case, Is.Null);
            Assert.That(comp.LastNet, Is.EqualTo(-500));
            Assert.That(entMan.HasComponent<PatientIntakePatientComponent>(patient), Is.False, "The body becomes an ordinary one.");
        });
    }

    [Test]
    public async Task GibbingThePatientFailsTheCase()
    {
        var (machine, patient) = await StartCase("PatientIntakeTestHealthy");
        var entMan = Pair.Server.EntMan;
        await Pair.Server.WaitPost(() => entMan.System<GibbingSystem>().Gib(patient));
        await Pair.RunTicksSync(5);
        await Pair.Server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            Assert.That(comp.Case, Is.Null);
            Assert.That(comp.LastNet, Is.EqualTo(-500));
        });
    }

    [Test]
    public async Task DestroyingTheMachineVoidsTheCase()
    {
        var (machine, patient) = await StartCase("PatientIntakeTestHealthy");
        var entMan = Pair.Server.EntMan;
        await Pair.Server.WaitPost(() =>
        {
            entMan.System<EntityStorageSystem>().OpenStorage(machine);
            entMan.DeleteEntity(machine);
        });
        await Pair.RunTicksSync(5);
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(patient), Is.True, "The body stays.");
            Assert.That(entMan.HasComponent<PatientIntakePatientComponent>(patient), Is.False);
        });
    }

    [Test]
    public async Task SwitchingOnNeedsMedicalAccessButOffDoesNot()
    {
        var (machine, _) = await StartCase("PatientIntakeTestHealthy");
        var entMan = Pair.Server.EntMan;
        await Pair.Server.WaitAssertion(() =>
        {
            var intake = entMan.System<PatientIntakeSystem>();
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            var coords = entMan.GetComponent<TransformComponent>(machine).Coordinates;
            var stranger = entMan.SpawnEntity("MobHuman", coords);

            intake.TryToggle((machine, comp), stranger, false);
            Assert.That(comp.Enabled, Is.False, "Anyone may switch it off.");

            intake.TryToggle((machine, comp), stranger, true);
            Assert.That(comp.Enabled, Is.False, "No medical access, no switching on.");

            var medic = entMan.SpawnEntity("MobHuman", coords);
            var id = entMan.SpawnEntity("MedicalIDCard", coords);
            entMan.System<SharedHandsSystem>().TryPickupAnyHand(medic, id);
            intake.TryToggle((machine, comp), medic, true);
            Assert.That(comp.Enabled, Is.True);
        });
    }

    [Test]
    public async Task LinkedFaxPrintsTheCaseFile()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid machine = default, fax = default;
        await server.WaitPost(() =>
        {
            AddAir(entMan, map.MapUid);
            entMan.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(0, -1), map.Tile.Tile);
            machine = entMan.SpawnEntity("PatientIntakeMachine", map.GridCoords);
            fax = entMan.SpawnEntity("FaxMachineBase", map.GridCoords);
            entMan.System<SharedDeviceLinkSystem>().LinkDefaults(null, machine, fax);

            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            entMan.System<PatientIntakeSystem>().StartCase((machine, comp), entMan.GetComponent<EntityStorageComponent>(machine),
                server.ProtoMan.Index<PatientIntakeCasePrototype>("PatientIntakeTestHealthy"));
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var printed = entMan.GetComponent<FaxMachineComponent>(fax).PrintingQueue.Count;
            var papers = entMan.GetComponent<EntityStorageComponent>(machine).Contents.ContainedEntities
                .Count(e => entMan.HasComponent<PaperComponent>(e));
            Assert.That(printed + papers, Is.GreaterThan(0), "The case file must be produced somewhere.");
            Assert.That(papers, Is.Zero, "With a fax linked, the case file goes to the fax, not the tray.");
        });
    }

    [Test]
    public async Task StartCommandStartsTheRequestedCase()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid machine = default;
        await server.WaitPost(() =>
        {
            AddAir(entMan, map.MapUid);
            machine = entMan.SpawnEntity("PatientIntakeMachine", map.GridCoords);
            var console = server.ResolveDependency<Robust.Shared.Console.IConsoleHost>();
            console.ExecuteCommand($"patientintake_start {entMan.GetNetEntity(machine)} PatientIntakeTestPoisoned");
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            Assert.That(comp.Case, Is.Not.Null, "No medic online and no timer: the command must start it anyway.");
            Assert.That(comp.Case!.Proto.Id, Is.EqualTo("PatientIntakeTestPoisoned"));
        });
    }

    [Test]
    public async Task BlacklistedSpeciesAreNeverRolled()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var caseProto = server.ProtoMan.Index<PatientIntakeCasePrototype>("PatientIntakeTestHealthy");

        await server.WaitAssertion(() =>
        {
            var intake = entMan.System<PatientIntakeSystem>();
            var blacklist = server.ProtoMan.Index<Content.Shared.Humanoid.Prototypes.RandomHumanoidSettingsPrototype>("PatientIntakePatient").SpeciesBlacklist;
            Assert.That(blacklist, Is.Not.Empty);

            for (var i = 0; i < 60; i++)
            {
                var machine = entMan.SpawnEntity("PatientIntakeMachine", map.GridCoords);
                var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
                intake.StartCase((machine, comp), entMan.GetComponent<EntityStorageComponent>(machine), caseProto);
                var patient = comp.Case!.Patients.First();
                var species = entMan.GetComponent<Content.Shared.Humanoid.HumanoidProfileComponent>(patient).Species;
                Assert.That(blacklist, Does.Not.Contain(species.Id));
                entMan.DeleteEntity(machine);
                entMan.DeleteEntity(patient);
            }
        });
    }

    [Test]
    public async Task ReceiptItemisesTimeBasePayAndMissingEffects()
    {
        var (machine, patient) = await StartCase("PatientIntakeTestHealthy");
        var server = Pair.Server;
        var entMan = server.EntMan;
        var storage = entMan.System<EntityStorageSystem>();

        await server.WaitPost(() => storage.OpenStorage(machine));
        await Pair.RunTicksSync(5);
        await server.WaitPost(() =>
        {
            // Keep one issued item out of the tray.
            var item = entMan.GetComponent<PatientIntakePatientComponent>(patient).Gear.First();
            entMan.System<SharedTransformSystem>().SetCoordinates(item, new EntityCoordinates(machine, new Vector2(5, 5)));
            storage.CloseStorage(machine);
        });
        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var receipt = string.Join("\n", entMan.EntityQuery<PaperComponent>().Select(p => p.Content));
            var loc = server.ResolveDependency<Robust.Shared.Localization.ILocalizationManager>();
            Assert.That(receipt, Does.Contain(loc.GetString("patient-intake-receipt-base", ("amount", 1000))));
            var gearPrefix = loc.GetString("patient-intake-receipt-gear", ("count", 1), ("amount", 0), ("percent", 0));
            gearPrefix = gearPrefix[..gearPrefix.IndexOf("-0", System.StringComparison.Ordinal)];
            Assert.That(receipt, Does.Contain(gearPrefix), "The missing-effects cut is listed with its amount.");

            var net = entMan.GetComponent<PatientIntakeMachineComponent>(machine).LastNet;
            Assert.That(net, Is.InRange(200, 550), "A 50-75% cut off ~1000 spesos.");
        });
    }

    [Test]
    public async Task EveryOutfitItemCanBeWornByAHuman()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var machine = entMan.SpawnEntity("PatientIntakeMachine", map.GridCoords);
            var inventory = entMan.System<Content.Shared.Inventory.InventorySystem>();
            foreach (var group in entMan.GetComponent<PatientIntakeMachineComponent>(machine).Outfits)
            {
                foreach (var (slot, options) in server.ProtoMan.Index(group).Slots)
                {
                    foreach (var itemId in options.Items)
                    {
                        // A jumpsuit first, since some slots need one underneath.
                        var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
                        if (slot != "jumpsuit")
                            inventory.TryEquip(human, entMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords), "jumpsuit", true, true);

                        var item = entMan.SpawnEntity(itemId, map.GridCoords);
                        Assert.That(inventory.TryEquip(human, item, slot, true, true), Is.True, $"{group}: {itemId} can't be worn in {slot}.");
                        entMan.DeleteEntity(human);
                    }
                }
            }
        });
    }

    [Test]
    public async Task MarkMissingDebitsAndReleasesThePatient()
    {
        var (machine, patient) = await StartCase("PatientIntakeTestHealthy");
        var server = Pair.Server;
        var entMan = server.EntMan;
        await server.WaitPost(() =>
        {
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            entMan.System<PatientIntakeSystem>().MarkMissing((machine, comp), comp.Case!);
        });
        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            Assert.That(comp.Case, Is.Null);
            Assert.That(comp.LastNet, Is.EqualTo(-500));
            Assert.That(entMan.HasComponent<PatientIntakePatientComponent>(patient), Is.False);
            Assert.That(entMan.GetComponent<MetaDataComponent>(patient).EntityPaused, Is.False);
        });
    }

    private async Task<(EntityUid Machine, EntityUid Patient)> StartCase(string caseId, TimeSpan? popDelay = null)
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid machine = default;
        EntityUid patient = default;
        await server.WaitPost(() =>
        {
            AddAir(entMan, map.MapUid);

            // The tray slides out onto the tile south of the machine.
            entMan.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(0, -1), map.Tile.Tile);
            machine = entMan.SpawnEntity("PatientIntakeMachine", map.GridCoords);
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            comp.PopDelay = popDelay ?? comp.PopDelay;
            var storage = entMan.GetComponent<EntityStorageComponent>(machine);
            entMan.System<PatientIntakeSystem>().StartCase((machine, comp), storage, server.ProtoMan.Index<PatientIntakeCasePrototype>(caseId));
            patient = comp.Case!.Patients.First();
        });
        await pair.RunTicksSync(5);
        return (machine, patient);
    }

    /// <summary>
    /// Breathable air, so live patients don't pick up vacuum damage between treatment and the check.
    /// </summary>
    public static void AddAir(IEntityManager entMan, EntityUid map)
    {
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 21.824779f;
        moles[(int) Gas.Nitrogen] = 82.10312f;
        entMan.System<AtmosphereSystem>().SetMapAtmosphere(map, false, new GasMixture(moles, Atmospherics.T20C));
    }

    private async Task OpenAndClose(EntityUid machine)
    {
        var storage = Pair.Server.EntMan.System<EntityStorageSystem>();
        await Pair.Server.WaitPost(() => storage.OpenStorage(machine));
        await Pair.RunTicksSync(5);
        await Pair.Server.WaitPost(() => storage.CloseStorage(machine));
        await Pair.RunTicksSync(5);
    }

    private static HashSet<ProtoId<OrganCategoryPrototype>> PartCategories(IEntityManager entMan, EntityUid patient)
    {
        return entMan.GetComponent<BodyComponent>(patient).Organs!.ContainedEntities
            .Select(o => entMan.GetComponent<OrganComponent>(o).Category)
            .OfType<ProtoId<OrganCategoryPrototype>>()
            .ToHashSet();
    }
}

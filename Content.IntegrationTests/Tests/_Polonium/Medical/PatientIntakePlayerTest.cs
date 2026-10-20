using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Medical.PatientIntake;
using Content.Server.Atmos.EntitySystems;
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

/// <summary>
/// Tests that attach the test client's player, so the client really receives the patients.
/// The pair is discarded afterwards: the session stays attached to entities this test deleted.
/// </summary>
[TestFixture]
[TestOf(typeof(PatientIntakeSystem))]
public sealed class PatientIntakePlayerTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task MachineStartsACaseOnlyWithAMedicOnline()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid machine = default;
        await server.WaitPost(() =>
        {
            PatientIntakeMachineTest.AddAir(entMan, map.MapUid);
            machine = entMan.SpawnEntity("PatientIntakeMachine", map.GridCoords);
            entMan.System<SharedPowerReceiverSystem>().SetNeedsPower(machine, false);
            entMan.GetComponent<PatientIntakeMachineComponent>(machine).UntilNextCase = TimeSpan.Zero;
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<PatientIntakeMachineComponent>(machine).Case, Is.Null, "Nobody from medical is online."));

        await server.WaitPost(() =>
        {
            // The test client's player becomes a living medical doctor.
            var medic = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var minds = entMan.System<SharedMindSystem>();
            var mind = minds.CreateMind(ServerSession!.UserId);
            minds.TransferTo(mind, medic);
            entMan.System<SharedRoleSystem>().MindAddJobRole(mind, silent: true, jobPrototype: "MedicalDoctor");
            entMan.GetComponent<PatientIntakeMachineComponent>(machine).UntilNextCase = TimeSpan.Zero;
        });
        // The machine rechecks every few seconds while it is due.
        await pair.RunSeconds(6);
        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
            Assert.That(comp.Case, Is.Not.Null, "A medic is online and the tray is empty: a case should arrive.");
            Assert.That(server.ProtoMan.Index(comp.Case!.Proto).MinMedics, Is.LessThanOrEqualTo(1),
                "With one medic, only cases needing one medic may roll.");
        });
    }

    [Test]
    public async Task PatientsSpawnCleanlyInViewOfAPlayer()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var caseProto = server.ProtoMan.Index<PatientIntakeCasePrototype>("PatientIntakeTestHealthy");

        await server.WaitPost(() =>
        {
            PatientIntakeMachineTest.AddAir(entMan, map.MapUid);
            var player = entMan.SpawnEntity("MobHuman", map.GridCoords);
            entMan.System<SharedMindSystem>().ControlMob(ServerSession!.UserId, player);
        });

        // Random species each time; the client must initialize every one of them without errors.
        for (var i = 0; i < 12; i++)
        {
            await server.WaitPost(() =>
            {
                var machine = entMan.SpawnEntity("PatientIntakeMachine", map.GridCoords);
                var comp = entMan.GetComponent<PatientIntakeMachineComponent>(machine);
                entMan.System<PatientIntakeSystem>().StartCase((machine, comp), entMan.GetComponent<EntityStorageComponent>(machine), caseProto);
                entMan.System<EntityStorageSystem>().OpenStorage(machine);
            });
            await pair.RunTicksSync(10);
        }
    }
}

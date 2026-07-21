using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Buckle;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
[TestOf(typeof(SharedSurgerySystem))]
public sealed class OperatingTableSpeedBonusTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OperatingTableSpeedTestPatient
  components:
  - type: Buckle

- type: entity
  id: OperatingTableSpeedTestStep
  parent: SurgeryStepBase
  components:
  - type: SurgeryStep
    tool: []
    duration: 4
";

    [Test]
    public async Task RealOperatingTablePrototypeAppliesItsSpeedBonus()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid patient = default;
        EntityUid table = default;
        EntityUid stepEnt = default;

        await server.WaitPost(() =>
        {
            patient = sEntMan.SpawnEntity("OperatingTableSpeedTestPatient", coords);
            table = sEntMan.SpawnEntity("OperatingTable", coords);
            stepEnt = sEntMan.SpawnEntity("OperatingTableSpeedTestStep", coords);
        });

        await pair.RunTicksSync(5);

        var getDurationMethod = typeof(SharedSurgerySystem).GetMethod("GetSurgeryDuration", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedSurgerySystem>();

        float durationWithoutTable = default;
        float durationWithTable = default;

        await server.WaitAssertion(() =>
        {
            durationWithoutTable = (float) getDurationMethod.Invoke(sSurgery, new object[] { stepEnt, patient, patient, 1f })!;
        });

        var sBuckle = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedBuckleSystem>();
        await server.WaitPost(() =>
        {
            Assert.That(sBuckle.TryBuckle(patient, null, table, popup: false), Is.True, "Sanity check: buckling to the table should succeed.");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            durationWithTable = (float) getDurationMethod.Invoke(sSurgery, new object[] { stepEnt, patient, patient, 1f })!;

            Assert.That(durationWithTable, Is.LessThan(durationWithoutTable),
                "Being buckled to the real OperatingTable prototype should speed up the surgery step (shorter duration) - it used to make no difference at all since nothing attached OperatingTableComponent to it.");
        });
    }
}

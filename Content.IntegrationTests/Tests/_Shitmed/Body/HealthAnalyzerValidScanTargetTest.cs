using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Medical;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// Code-review finding: HealthAnalyzerSystem.UpdateScannedUser's doll+tabs UI requires
/// BodyComponent (Bleeding/Tourniqueted/UnfinishedSurgery/Traumas are all organ-derived), but
/// ValidScanTarget only required MobStateComponent - scanning an animal/xeno/silicon with no
/// BodyComponent let the DoAfter start (sound, popup) and run its full delay before silently
/// sending nothing at all, leaving the window on its placeholder forever. Fixed by also
/// requiring BodyComponent in ValidScanTarget, so the analyzer simply refuses to start scanning
/// something it has no organ-based UI for.
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class HealthAnalyzerValidScanTargetTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ValidScanTargetBodylessMob
  components:
  - type: MobState

- type: entity
  id: ValidScanTargetRealMob
  components:
  - type: MobState
  - type: Body
";

    [Test]
    public async Task RejectsMobStateOnlyTargetsButAcceptsOnesWithABody()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sHealthAnalyzer = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<HealthAnalyzerSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid bodylessMob = default;
        EntityUid realMob = default;

        await server.WaitPost(() =>
        {
            bodylessMob = sEntMan.SpawnEntity("ValidScanTargetBodylessMob", coords);
            realMob = sEntMan.SpawnEntity("ValidScanTargetRealMob", coords);
        });

        await pair.RunTicksSync(5);

        var validScanTargetMethod = typeof(HealthAnalyzerSystem).GetMethod("ValidScanTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await server.WaitAssertion(() =>
        {
            var bodylessResult = (bool) validScanTargetMethod.Invoke(sHealthAnalyzer, new object[] { bodylessMob })!;
            Assert.That(bodylessResult, Is.False, "A MobState-only target with no BodyComponent should be rejected up front, not let the DoAfter start.");

            var realResult = (bool) validScanTargetMethod.Invoke(sHealthAnalyzer, new object[] { realMob })!;
            Assert.That(realResult, Is.True, "A real target with both MobState and Body should still be accepted.");
        });
    }
}

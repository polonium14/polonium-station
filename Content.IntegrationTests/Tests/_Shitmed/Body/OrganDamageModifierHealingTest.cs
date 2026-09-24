using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
public sealed class OrganDamageModifierHealingTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OrganModifierHealingTestOrgan
  components:
  - type: OrganIntegrity
    integrityCap: 100
    integrityThresholds:
      Normal: 100
      Damaged: 50
      Destroyed: 0
";

    [Test]
    public async Task RemovingLastModifierRestoresFullIntegrity()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var organ = SEntMan.SpawnEntity("OrganModifierHealingTestOrgan", coords);
            var owner = SEntMan.SpawnEntity(null, coords);
            var traumas = SEntMan.System<TraumaSystem>();
            var integrity = SEntMan.GetComponent<OrganIntegrityComponent>(organ);

            Assert.That(traumas.TryCreateOrganDamageModifier(organ, FixedPoint2.New(20), owner, "TestDamage"), Is.True);
            Assert.That(integrity.OrganIntegrity, Is.EqualTo(FixedPoint2.New(80)));
            Assert.That(traumas.TryRemoveOrganDamageModifier(organ, owner, "TestDamage"), Is.True);
            Assert.That(integrity.IntegrityModifiers, Is.Empty);
            Assert.That(integrity.OrganIntegrity, Is.EqualTo(integrity.IntegrityCap));
            Assert.That(integrity.OrganSeverity, Is.EqualTo(OrganSeverity.Normal));
        });
    }
}

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Buckle.Components;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// User request: "when the patient is unbuckled from the surgery bed maybe they should slowly
/// start to bleed?" - penalizes getting up with unfinished surgery (see
/// HealthAnalyzerUnfinishedSurgeryTest for the marker-detection half of this feature).
///
/// UnfinishedSurgeryPenaltySystem's OnUnbuckled subscribes UnbuckledEvent directly - this test
/// raises that event directly rather than driving the full SharedBuckleSystem interaction/range
/// checks (same "test the handler, not the engine's own buckle machinery" approach
/// HealthAnalyzerTourniquetTest already uses for TourniquetDoAfterEvent).
/// </summary>
[TestFixture]
[TestOf(typeof(UnfinishedSurgeryPenaltySystem))]
public sealed class UnfinishedSurgeryBleedingPenaltyTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: UnfinishedSurgeryPenaltyMob
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: Buckle

- type: entity
  id: UnfinishedSurgeryPenaltyArm
  components:
  - type: Organ
    category: ArmLeft
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 80
    thresholds:
      Healthy: 80
      Minor: 64
      Moderate: 48
      Severe: 32
      Critical: 16
      Mangled: 6
      Severed: 0

- type: entity
  id: UnfinishedSurgeryPenaltyBed
  components:
  - type: Strap
  - type: HealOnBuckle
    damage:
      types:
        Poison: -0.1

- type: entity
  id: UnfinishedSurgeryPenaltyChair
  components:
  - type: Strap
";

    [Test]
    public async Task UnbuckleFromBedWithUnfinishedSurgeryStartsBleeding()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid mob = default;
        EntityUid arm = default;
        EntityUid bed = default;

        await server.WaitPost(() =>
        {
            mob = sEntMan.SpawnEntity("UnfinishedSurgeryPenaltyMob", coords);
            arm = sEntMan.SpawnEntity("UnfinishedSurgeryPenaltyArm", coords);
            bed = sEntMan.SpawnEntity("UnfinishedSurgeryPenaltyBed", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(mob, BodyComponent.ContainerID);
            container.Insert(arm, organsContainer);

            // Leave an incision open - unfinished surgery.
            sEntMan.AddComponent<IncisionOpenComponent>(arm);
        });

        await pair.RunTicksSync(5);

        // Raise UnbuckledEvent directly, as if the mob just got up off the bed.
        await server.WaitPost(() =>
        {
            var strapEnt = new Entity<StrapComponent>(bed, sEntMan.GetComponent<StrapComponent>(bed));
            var buckleEnt = new Entity<BuckleComponent>(mob, sEntMan.GetComponent<BuckleComponent>(mob));
            var ev = new UnbuckledEvent(strapEnt, buckleEnt);
            sEntMan.EventBus.RaiseLocalEvent(mob, ref ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.HasComponent<UnfinishedSurgeryPenaltyComponent>(mob), Is.True,
                "Unbuckling with unfinished surgery from a HealOnBuckle-bearing bed should start the penalty.");
        });

        // Run past several tick intervals (3s each) so the penalty system's Update() actually fires.
        await pair.RunTicksSync((int) (15 / SGameTiming.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var bloodstream = sEntMan.GetComponent<BloodstreamComponent>(mob);
            Assert.That(bloodstream.BleedAmount, Is.GreaterThan(0),
                "The mob should now be visibly bleeding (BleedAmount > 0, drives the HUD alert and eventual puddle) as a penalty.");
        });

        // Close the incision - surgery is finished, penalty should stop and remove itself.
        await server.WaitPost(() => sEntMan.RemoveComponent<IncisionOpenComponent>(arm));

        await pair.RunTicksSync((int) (15 / pair.Server.Timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.HasComponent<UnfinishedSurgeryPenaltyComponent>(mob), Is.False,
                "Finishing the surgery should remove the penalty tracking component.");

            var bloodstream = sEntMan.GetComponent<BloodstreamComponent>(mob);
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(0f),
                "Finishing the surgery should also stop the bleed the penalty applied - the incision is closed.");
        });
    }

    [Test]
    public async Task UnbuckleFromPlainChairNeverStartsThePenalty()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid mob = default;
        EntityUid arm = default;
        EntityUid chair = default;

        await server.WaitPost(() =>
        {
            mob = sEntMan.SpawnEntity("UnfinishedSurgeryPenaltyMob", coords);
            arm = sEntMan.SpawnEntity("UnfinishedSurgeryPenaltyArm", coords);
            chair = sEntMan.SpawnEntity("UnfinishedSurgeryPenaltyChair", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(mob, BodyComponent.ContainerID);
            container.Insert(arm, organsContainer);

            sEntMan.AddComponent<IncisionOpenComponent>(arm);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var strapEnt = new Entity<StrapComponent>(chair, sEntMan.GetComponent<StrapComponent>(chair));
            var buckleEnt = new Entity<BuckleComponent>(mob, sEntMan.GetComponent<BuckleComponent>(mob));
            var ev = new UnbuckledEvent(strapEnt, buckleEnt);
            sEntMan.EventBus.RaiseLocalEvent(mob, ref ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.HasComponent<UnfinishedSurgeryPenaltyComponent>(mob), Is.False,
                "A plain chair (no HealOnBuckleComponent) should never trigger the bleed-on-unbuckle penalty.");
        });
    }
}

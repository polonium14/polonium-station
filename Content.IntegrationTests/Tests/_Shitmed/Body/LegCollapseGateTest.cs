using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;


[TestFixture]
public sealed class LegCollapseGateTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: LegGateTestBorgVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: StandingState
  - type: MovementSpeedModifier

- type: entity
  id: LegGateTestUncategorizedOrgan
  components:
  - type: Organ
";

    [Test]
    public async Task LegFreeSpeciesIsNotForcedDownAndCanStand()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid snail = default;

        await server.WaitPost(() =>
        {
            snail = sEntMan.SpawnEntity("MobGastropoid", coords);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var standing = sEntMan.System<StandingStateSystem>();
            Assert.That(standing.IsDown(snail), Is.False,
                "A leg-free species should not be forced down just by existing.");

            sEntMan.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(snail);
            Assert.That(standing.IsDown(snail), Is.False,
                "A movement-speed refresh must not force a leg-free species down.");

            sEntMan.System<SharedStunSystem>().TryKnockdown(snail, TimeSpan.FromSeconds(0.5), force: true);
            Assert.That(standing.IsDown(snail), Is.True,
                "Setup failure: the knockdown didn't actually put the mob down.");
        });

        // 0.5s knockdown + 2s stand-up DoAfter at 30 tps, with generous margin.
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.System<StandingStateSystem>().IsDown(snail), Is.False,
                "A leg-free species must be able to stand back up after a knockdown.");
        });
    }

    [Test]
    public async Task BodyWithoutManifestIsNotStandLocked()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            // Borg-shaped: a bare Body plus an uncategorized organ (their brain), no InitialBody.
            victim = sEntMan.SpawnEntity("LegGateTestBorgVictim", coords);
            var organ = sEntMan.SpawnEntity("LegGateTestUncategorizedOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            container.Insert(organ, container.GetContainer(victim, BodyComponent.ContainerID));

            sEntMan.System<SharedStunSystem>().TryKnockdown(victim, TimeSpan.FromSeconds(0.5), force: true);
        });

        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.System<StandingStateSystem>().IsDown(victim), Is.False,
                "A Body-haver with no InitialBody manifest (borg-like) must not be stand-locked after a knockdown.");
        });
    }

    [Test]
    public async Task LegDependentSpeciesStillCollapsesWithoutLegs()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid human = default;

        await server.WaitPost(() =>
        {
            human = sEntMan.SpawnEntity("MobHuman", coords);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var body = sEntMan.GetComponent<BodyComponent>(human);
            var legs = new List<EntityUid>();
            foreach (var organ in body.Organs!.ContainedEntities)
            {
                if (sEntMan.TryGetComponent(organ, out OrganComponent organComp)
                    && organComp.Category?.Id is "LegLeft" or "LegRight")
                {
                    legs.Add(organ);
                }
            }

            Assert.That(legs, Has.Count.EqualTo(2), "Setup failure: expected a human to have two legs.");
            foreach (var leg in legs)
            {
                sEntMan.DeleteEntity(leg);
            }
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            sEntMan.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(human);
            Assert.That(sEntMan.System<StandingStateSystem>().IsDown(human), Is.True,
                "A leg-dependent species with both legs gone must still collapse.");
        });
    }
}

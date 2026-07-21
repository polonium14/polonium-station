using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Systems;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// Code-review finding: PainSystem.Damage.cs's UpdateDamage (run every tick, per entity with a
/// NerveSystemComponent, via PainTimerJob) mutated PainSoundsToPlay/Modifiers/Multipliers while
/// iterating those exact same dictionaries - the first tick after a timed pain modifier or
/// multiplier expires threw InvalidOperationException, permanently aborting that entity's pain
/// processing job (caught/logged by JobQueue, not a full server crash, but silent and
/// permanent). No shipped content currently sets a timed modifier/multiplier (TraumaSystem's
/// NerveDamage case is the one real caller with a time value), so this is exercised directly via
/// the public TryAddPainMultiplier API rather than waiting on a specific trauma roll. Fixed by
/// snapshotting each enumeration with .ToList() first, same pattern as the sibling fix in
/// ConsciousnessSystem.Process.cs.
/// </summary>
[TestFixture]
[TestOf(typeof(PainSystem))]
public sealed class PainTimedModifierExpiryTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: PainExpiryTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: Consciousness
    threshold: 95
    cap: 190

- type: entity
  id: PainExpiryTestBrainOrgan
  components:
  - type: Organ
    category: Head
  - type: ConsciousnessRequired
    identifier: nerveSystem
    causesDeath: true
  - type: NerveSystem
  - type: OrganIntegrity
    integrityCap: 15
    integrityThresholds:
      Normal: 15
      Damaged: 6
      Destroyed: 0
";

    [Test]
    public async Task ExpiredTimedPainMultiplierDoesNotThrow()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sPain = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<PainSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            var victim = sEntMan.SpawnEntity("PainExpiryTestVictim", coords);
            brain = sEntMan.SpawnEntity("PainExpiryTestBrainOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            // Short expiry - the very next tick after this should try to remove it while
            // UpdateDamage's own loop is iterating the same dictionary.
            Assert.That(sPain.TryAddPainMultiplier(brain, "TestExpiry", FixedPoint2.New(1), time: TimeSpan.FromSeconds(0.1)), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            var nerveSys = sEntMan.GetComponent<NerveSystemComponent>(brain);
            Assert.That(nerveSys.Multipliers, Does.ContainKey("TestExpiry"), "Sanity check: the multiplier should actually be present before it expires.");
        });

        // Run past the expiry - every tick enqueues a PainTimerJob for every NerveSystemComponent
        // entity (PainSystem.cs's own Update loop), so this is enough ticks for both the 0.1s
        // real-time expiry and the job queue to actually process it.
        await pair.RunTicksSync(60);

        // This used to throw and permanently break the entity's pain processing - if it throws
        // here (surfacing via the job queue's own exception logging turning into a test
        // assertion failure below, or directly), the regression has returned.
        await server.WaitAssertion(() =>
        {
            var nerveSys = sEntMan.GetComponent<NerveSystemComponent>(brain);
            Assert.That(nerveSys.Multipliers, Does.Not.ContainKey("TestExpiry"), "The expired multiplier should have been removed.");
        });
    }
}

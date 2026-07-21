using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

/// <summary>
/// Live client crash report: pressing a surgery on an organ whose category has no case in
/// CanPerformStep's category-to-SlotFlags switch (SharedSurgerySystem.Steps.cs) threw a
/// Robust.Shared.Utility.DebugAssertException out of InventorySystem.Slots.cs's
/// InventorySlotEnumerator ctor (DebugTools.Assert(flags != SlotFlags.NONE)), taking down the
/// whole client - the switch's default case fell through to SlotFlags.NONE, and
/// OnToolCanPerform passed that straight into InventorySystem.TryGetContainerSlotEnumerator with
/// no guard, so the very next line's ctor call hit the engine's own "don't enumerate with no
/// flags" assertion. Fixed by guarding OnToolCanPerform to skip the inventory check entirely
/// when TargetSlots is NONE, so any unmapped part category degrades to "nothing to check"
/// instead of crashing. Originally reported against a real "Groin" organ category (since
/// removed - the feet/legs/torso/groin body-part merge folded Groin back into Torso); this test
/// uses its own synthetic unmapped category so it keeps covering the general fix.
/// </summary>
[TestFixture]
public sealed class GroinSurgeryArmorCheckCrashTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: organCategory
  id: UnmappedCrashTestCategory

- type: entity
  id: GroinCrashTestBody
  components:
  - type: Body
  - type: Inventory
  - type: SurgeryTarget

- type: entity
  id: GroinCrashTestGroinOrgan
  components:
  - type: Organ
    category: UnmappedCrashTestCategory
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 100
    healAbility: 0
    thresholds:
      Healthy: 100
      Minor: 80
      Moderate: 60
      Severe: 40
      Critical: 20
      Mangled: 7
      Severed: 0

- type: entity
  id: GroinCrashTestSurgery
  components:
  - type: Surgery
    steps:
    - GroinCrashTestStep

- type: entity
  id: GroinCrashTestStep
  components:
  - type: SurgeryStep
    duration: 1
    add:
    - type: IncisionOpen
";

    [Test]
    public async Task CanPerformStepOnUnmappedCategoryOrganDoesNotThrow()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid body = default;
        EntityUid groin = default;

        await server.WaitPost(() =>
        {
            body = sEntMan.SpawnEntity("GroinCrashTestBody", coords);
            groin = sEntMan.SpawnEntity("GroinCrashTestGroinOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(body, BodyComponent.ContainerID);
            container.Insert(groin, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var stepEnt = sSurgery.GetSingleton("GroinCrashTestStep");
            Assert.That(stepEnt, Is.Not.Null);

            Assert.DoesNotThrow(() =>
                    sSurgery.CanPerformStepWithHeld(body, body, groin, stepEnt!.Value, false, out _),
                "OnToolCanPerform previously crashed with a DebugAssertException here for any part " +
                "category missing from CanPerformStep's switch - the live client crash this test guards against.");
        });
    }
}

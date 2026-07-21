using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Systems;
using Content.Shared.Body;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// User report: "after the pawn died he stopped receiving internal organ damage when i was
/// still hitting him". Root cause: OnOrganAdded (ConsciousnessSystem.Process.cs) sets
/// ConsciousnessComponent.NerveSystem when the "nerveSystem"-identified organ (the brain) is
/// inserted, but OnOrganRemoved never undid it. When the brain is the organ that actually gets
/// destroyed - the common causesDeath:true killing blow, and exactly what a real player death
/// usually is - NerveSystem kept pointing at the now-deleted brain entity.
/// TryGetNerveSystem's existing `Comp is null` guard (added for OrganDestroyedNoNerveSystemTest)
/// only catches a NerveSystem that was NEVER populated - a stale reference to something that WAS
/// valid and then got deleted still has a non-null Comp, so it kept returning true. Every
/// subsequent trauma roll (ApplyTraumas dereferences nerveSys.Value.Comp/.Owner for its pain
/// modifiers, for bone/organ/nerve damage alike) then dereferenced a deleted entity - explaining
/// why organ damage (and likely all trauma types) went dead silent right at the moment of death.
/// Fixed by resetting NerveSystem to default when its organ is removed, matching how OnOrganAdded
/// sets it in the first place.
/// </summary>
[TestFixture]
[TestOf(typeof(ConsciousnessSystem))]
public sealed class NerveSystemStaleAfterBrainRemovedTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: StaleNerveSystemTestVictim
  components:
  - type: Body
  - type: Consciousness
    threshold: 95
    cap: 190

- type: entity
  id: StaleNerveSystemTestBrainOrgan
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
    public async Task NerveSystemStopsResolvingOnceItsBrainOrganIsRemoved()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sConsciousness = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<ConsciousnessSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("StaleNerveSystemTestVictim", coords);
            brain = sEntMan.SpawnEntity("StaleNerveSystemTestBrainOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sConsciousness.TryGetNerveSystem(victim, out _), Is.True,
                "Sanity check: with the brain organ inserted, the nerve system should resolve.");
        });

        // Simulate the brain being destroyed by cumulative organ damage - the same
        // container-remove-then-delete sequence TraumaSystem.Organs.cs's OnOrganSeverityChanged
        // performs when an organ's integrity hits zero.
        await server.WaitPost(() =>
        {
            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Remove(brain, organsContainer, force: true);
            sEntMan.DeleteEntity(brain);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sConsciousness.TryGetNerveSystem(victim, out _), Is.False,
                "Once the brain organ is destroyed, TryGetNerveSystem should stop resolving instead of returning a stale reference to the deleted organ - this used to keep returning true, silently breaking every subsequent trauma roll (bone/organ/nerve damage alike).");
        });
    }
}

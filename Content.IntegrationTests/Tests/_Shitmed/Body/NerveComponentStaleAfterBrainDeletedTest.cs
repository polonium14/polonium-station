using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared.Body;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// Production crash: "Can't resolve MetaDataComponent" on an entity during PVS state
/// serialization, this time traced to NerveComponent's own auto-networked state getter
/// (NerveComponent.ParentedNerveSystem, an EntityUid pointing at the brain/NerveSystemComponent
/// entity a limb's nerve is parented to). Root cause: PainSystem.cs's OnOrganInserted/
/// OnOrganRemoved are the only code that ever touch ParentedNerveSystem, and both bail out
/// immediately via `!_consciousness.TryGetNerveSystem(body, out var brainUid)` - which is
/// exactly the state right after the brain itself is destroyed (see
/// NerveSystemStaleAfterBrainRemovedTest for that same guard's own fix). Nothing ever swept
/// every OTHER limb's NerveComponent to clear a reference to a nerve system that no longer
/// exists, so every limb kept ParentedNerveSystem pointing at the deleted brain forever.
///
/// Fixed the same way as TraumaSystem.Cleanup.cs's generic hook: PainSystem now subscribes to
/// NerveSystemComponent's own EntityTerminatingEvent and clears ParentedNerveSystem on every
/// limb that was pointing at it, regardless of what caused the brain to be destroyed.
/// </summary>
[TestFixture]
[TestOf(typeof(Content.Shared._Shitmed.Medical.Surgery.Pain.Systems.PainSystem))]
public sealed class NerveComponentStaleAfterBrainDeletedTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: NerveStaleTestVictim
  components:
  - type: Body
  - type: Consciousness
    threshold: 95
    cap: 190

- type: entity
  id: NerveStaleTestBrainOrgan
  components:
  - type: Organ
    category: Head
  - type: ConsciousnessRequired
    identifier: nerveSystem
    causesDeath: true
  - type: NerveSystem

- type: entity
  id: NerveStaleTestTorsoOrgan
  components:
  - type: Organ
    category: Torso
  - type: Nerve
";

    [Test]
    public async Task ParentedNerveSystemIsClearedOnceTheBrainIsDeleted()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid brain = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("NerveStaleTestVictim", coords);
            brain = sEntMan.SpawnEntity("NerveStaleTestBrainOrgan", coords);
            torso = sEntMan.SpawnEntity("NerveStaleTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            // Brain first - resolving the nerve system requires it, same ordering as every
            // other brain+limb test in this suite.
            container.Insert(brain, organsContainer);
            container.Insert(torso, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var nerve = sEntMan.GetComponent<NerveComponent>(torso);
            Assert.That(nerve.ParentedNerveSystem, Is.EqualTo(brain), "Sanity check: the torso's nerve should be parented to the brain before it's destroyed.");
        });

        await server.WaitPost(() => sEntMan.DeleteEntity(brain));

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var nerve = sEntMan.GetComponent<NerveComponent>(torso);
            Assert.That(nerve.ParentedNerveSystem, Is.EqualTo(default(EntityUid)),
                "Once the brain is deleted, the torso's ParentedNerveSystem should be cleared, not left dangling pointing at a deleted entity - this used to crash PVS state serialization.");
        });
    }
}

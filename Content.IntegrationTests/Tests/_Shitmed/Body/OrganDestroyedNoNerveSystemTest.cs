using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.Body;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// Code-review finding: ConsciousnessSystem.Helpers.cs's TryGetNerveSystem returned true with a
/// null Entity&lt;NerveSystemComponent&gt;.Comp whenever the body has a ConsciousnessComponent
/// but its NerveSystem field was never actually populated (no nerve-bearing organ inserted yet,
/// or a body that never has one) - the default Entity&lt;T&gt; has Owner Invalid, Comp null.
/// TraumaSystem.Organs.cs's OnOrganSeverityChanged trusts the bool and dereferences
/// nerveSys.Value.Comp directly, so destroying an organ on such a body threw
/// NullReferenceException. Fixed by also checking Comp is not null before returning true.
/// </summary>
[TestFixture]
[TestOf(typeof(TraumaSystem))]
public sealed class OrganDestroyedNoNerveSystemTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: NoNerveSystemVictim
  components:
  - type: Body
  - type: Consciousness
    threshold: 95
    cap: 190

- type: entity
  id: NoNerveSystemTorsoOrgan
  components:
  - type: Organ
    category: Torso
  - type: OrganIntegrity
    integrityCap: 200
    integrityThresholds:
      Normal: 200
      Damaged: 80
      Destroyed: 0
";

    [Test]
    public async Task DestroyingAnOrganOnABodyWithNoNerveSystemDoesNotThrow()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("NoNerveSystemVictim", coords);
            // Deliberately no brain/NerveSystem organ inserted - ConsciousnessComponent exists
            // on the victim, but consciousness.NerveSystem stays at its default (Comp null).
            torso = sEntMan.SpawnEntity("NoNerveSystemTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
        });

        await pair.RunTicksSync(5);

        // This used to throw NullReferenceException inside OnOrganSeverityChanged - if it
        // throws here, the regression has returned.
        await server.WaitPost(() =>
        {
            var organComp = sEntMan.GetComponent<OrganComponent>(torso);
            var ev = new OrganDamageSeverityChangedOnWoundable(
                new Entity<OrganComponent>(torso, organComp),
                OrganSeverity.Damaged,
                OrganSeverity.Destroyed);
            sEntMan.EventBus.RaiseLocalEvent(victim, ref ev);
        });

        await pair.RunTicksSync(5);

        // If we got here without an unhandled exception, the fix held.
        Assert.Pass();
    }
}

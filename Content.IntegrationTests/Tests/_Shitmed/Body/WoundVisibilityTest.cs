using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.PartStatus;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
[TestOf(typeof(PartStatusSystem))]
public sealed class WoundVisibilityTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: WoundVisibilityTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: Consciousness
    threshold: 95
    cap: 190

- type: entity
  id: WoundVisibilityTestBrainOrgan
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

- type: entity
  id: WoundVisibilityTestTorsoOrgan
  components:
  - type: Organ
    category: Torso
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 200
    thresholds:
      Healthy: 200
      Minor: 160
      Moderate: 120
      Severe: 80
      Critical: 40
      Mangled: 14
      Severed: 0
";

    [Test]
    public async Task HandScannerVisibilityHidesFromExamineButShowsInAnalyzer()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sPartStatus = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<PartStatusSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity(null, coords);
            victim = sEntMan.SpawnEntity("WoundVisibilityTestVictim", coords);
            brain = sEntMan.SpawnEntity("WoundVisibilityTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("WoundVisibilityTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(organ, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: attacker);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            var wound = sWound.GetWoundableWounds(organ, woundableComp).First();
            var woundComp = sEntMan.GetComponent<WoundComponent>(wound);
            Assert.That(woundComp.WoundSeverity, Is.Not.EqualTo(WoundSeverity.Healed),
                "Setup didn't actually leave a real wound to test visibility against.");

            // Above the health analyzer's HandScanner tier - should be invisible to it too.
            woundComp.WoundVisibility = WoundVisibility.AdvancedScanner;
            var hiddenText = sPartStatus.GetPartStatusDescriptions(victim)[TargetBodyPart.Chest];
            Assert.That(hiddenText, Does.Contain("fine"),
                "An AdvancedScanner-tier wound should be hidden from a HandScanner-tier health analyzer scan.");

            // At the health analyzer's own tier - should now be visible.
            woundComp.WoundVisibility = WoundVisibility.HandScanner;
            var visibleText = sPartStatus.GetPartStatusDescriptions(victim)[TargetBodyPart.Chest];
            Assert.That(visibleText, Does.Not.Contain("fine"),
                "A HandScanner-tier wound should be visible to a HandScanner-tier health analyzer scan.");
        });
    }

    [Test]
    public async Task WoundGetsARealDamageGroupAndShowsInAnalyzer()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sPartStatus = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<PartStatusSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity(null, coords);
            victim = sEntMan.SpawnEntity("WoundVisibilityTestVictim", coords);
            brain = sEntMan.SpawnEntity("WoundVisibilityTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("WoundVisibilityTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(organ, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: attacker);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            var wound = sWound.GetWoundableWounds(organ, woundableComp).First();
            var woundComp = sEntMan.GetComponent<WoundComponent>(wound);

            Assert.That(woundComp.DamageGroup, Is.Not.Null,
                "A freshly-created wound should have its DamageGroup populated from its own DamageType.");
            Assert.That(woundComp.DamageGroup.Value.Id, Is.EqualTo("Brute"),
                "Blunt damage should resolve to the Brute damage group.");
            Assert.That(woundComp.WoundVisibility, Is.EqualTo(WoundVisibility.Always),
                "Sanity check: WoundBlunt's default visibility should still be Always (untouched by either fix).");

            var text = sPartStatus.GetPartStatusDescriptions(victim)[TargetBodyPart.Chest];
            Assert.That(text, Does.Not.Contain("fine"),
                "An ordinary Blunt wound's severity should now actually render in the health analyzer.");
        });
    }
}

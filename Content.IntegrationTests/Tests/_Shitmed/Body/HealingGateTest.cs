using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
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
[TestOf(typeof(Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems.WoundSystem))]
public sealed class HealingGateTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HealGateTestAttacker
  components:
  - type: Targeting

- type: entity
  id: HealGateTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable

- type: entity
  id: HealGateTestTorsoOrgan
  components:
  - type: Organ
    category: Torso
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 200
    healAbility: 5
    thresholds:
      Healthy: 200
      Minor: 160
      Moderate: 120
      Severe: 80
      Critical: 40
      Mangled: 14
      Severed: 0

- type: entity
  id: HealGateTestHeadOrgan
  components:
  - type: Organ
    category: Head
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 125
    healAbility: 5
    thresholds:
      Healthy: 125
      Minor: 100
      Moderate: 75
      Severe: 50
      Critical: 25
      Mangled: 9
      Severed: 0
";

    [Test]
    public async Task FreshHitOnOneLimbDelaysHealingOnAnUntouchedLimb()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems.WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid torso = default;
        EntityUid head = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("HealGateTestAttacker", coords);
            victim = sEntMan.SpawnEntity("HealGateTestVictim", coords);
            torso = sEntMan.SpawnEntity("HealGateTestTorsoOrgan", coords);
            head = sEntMan.SpawnEntity("HealGateTestHeadOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
            container.Insert(head, organsContainer);

            sEntMan.GetComponent<TargetingComponent>(attacker).Target = TargetBodyPart.Chest;
        });

        await pair.RunTicksSync(5);

        // Wound both limbs at once - torso via the targeted mob hit (bridges to the organ),
        // head dealt directly since there's only one TargetingComponent selection available.
        // The halt calls are belt-and-braces: WoundBlunt carries no BleedInflicter at all, so
        // these blunt wounds can't bleed to begin with. They stay in so the test keeps testing
        // the damage-time gate alone if the damage type or wound prototype ever changes - an
        // actively-bleeding wound is a separate blocker (CanHealWound) and would muddy this.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(10)), ignoreResistances: false, origin: attacker);
            sDamageable.TryChangeDamage(head, new DamageSpecifier(proto, FixedPoint2.New(10)), ignoreResistances: true, origin: attacker);
            sWound.TryHaltAllBleeding(torso, force: true);
            sWound.TryHaltAllBleeding(head, force: true);
        });

        await pair.RunTicksSync(5);

        // Just under the 2s window, hit the torso again - a fresh LastDamageTime for the torso
        // only. The head's own LastDamageTime is now ~1.5s old and would have unblocked it
        // under the old per-organ gate.
        await pair.RunSeconds(1.5f);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(1)), ignoreResistances: false, origin: attacker);
        });

        // Passive healing (WoundSystem.Healing.cs's TryHealWoundsOnWoundable) only ever reduces
        // wound severity / WoundableIntegrity - unlike HealingSystem.OnDoAfter (item-based), it
        // never calls DamageableSystem.TryChangeDamage, so the organ's/mob's raw damage never
        // moves from natural regen alone. Measure WoundableIntegrity, not GetTotalDamage.
        FixedPoint2 headIntegrityBefore = default;
        await server.WaitAssertion(() =>
        {
            headIntegrityBefore = sEntMan.GetComponent<WoundableComponent>(head).WoundableIntegrity;
        });

        // 1.5s later: 3s since the original head hit (would be healing under the old per-organ
        // gate) but only 1.5s since the fresh torso hit - body-wide gate should still be closed.
        await pair.RunSeconds(1.5f);

        await server.WaitAssertion(() =>
        {
            var headIntegrityAfter = sEntMan.GetComponent<WoundableComponent>(head).WoundableIntegrity;
            Assert.That(headIntegrityAfter, Is.EqualTo(headIntegrityBefore),
                "The head shouldn't start passively healing just because it wasn't the limb that took the most recent hit - a fresh hit anywhere should delay healing body-wide, matching Goob's own per-mob gate.");
        });

        // Past 2s since the fresh torso hit now - both limbs should be eligible.
        await pair.RunSeconds(1.5f);

        await server.WaitAssertion(() =>
        {
            var headIntegrityFinal = sEntMan.GetComponent<WoundableComponent>(head).WoundableIntegrity;
            Assert.That(headIntegrityFinal, Is.GreaterThan(headIntegrityBefore),
                "Once enough time has passed since the last hit anywhere on the body, healing should resume on every limb, including the one that wasn't hit last.");
        });
    }
}

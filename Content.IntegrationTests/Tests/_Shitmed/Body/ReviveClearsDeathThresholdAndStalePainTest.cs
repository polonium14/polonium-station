// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
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
public sealed class ReviveClearsDeathThresholdAndStalePainTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ReviveTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: MobState
  - type: MobThresholds
    allowRevives: true
    thresholds:
      0: Alive
      50: Critical
      100: Dead
  - type: Consciousness
    threshold: 45
    cap: 190

- type: entity
  id: ReviveTestBrainOrgan
  components:
  - type: Organ
    category: Head
  - type: ConsciousnessRequired
    identifier: nerveSystem
    causesDeath: true
  - type: NerveSystem

- type: entity
  id: ReviveTestTorsoOrgan
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
      Minor: 190
      Moderate: 170
      Severe: 140
      Critical: 100
      Mangled: 50
      Severed: 0
";

    [Test]
    public async Task DeathThresholdModifierAndStalePainAreBothClearedOnRevive()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default, brain = default, torso = default;
        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("ReviveTestVictim", coords);
            brain = sEntMan.SpawnEntity("ReviveTestBrainOrgan", coords);
            torso = sEntMan.SpawnEntity("ReviveTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);

            container.Insert(brain, organsContainer);
            container.Insert(torso, organsContainer);
        });

        await pair.RunTicksSync(5);

        var proto = sProtoMan.Index(BluntDamageType);
        await server.WaitPost(() =>
        {
            sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(150)), ignoreResistances: true, interruptsDoAfters: false);
        });

        await pair.RunTicksSync(5);

        var sMobState = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<Content.Shared.Mobs.Systems.MobStateSystem>();
        var sConsciousness = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<Content.Shared._Shitmed.Medical.Surgery.Consciousness.Systems.ConsciousnessSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(sMobState.IsDead(victim), Is.True, "Sanity check: the hit should have killed the mob.");

            var nerveSys = sEntMan.GetComponent<NerveSystemComponent>(brain);
            Assert.That(nerveSys.Pain, Is.GreaterThan(FixedPoint2.Zero), "Sanity check: the wound should have registered real pain.");

            var consciousness = sEntMan.GetComponent<Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components.ConsciousnessComponent>(victim);
            Assert.That(consciousness.Modifiers.ContainsKey((victim, "DeathThreshold")), Is.True,
                "Sanity check: dying should have added the DeathThreshold modifier.");

            TestContext.Out.WriteLine($"[DEBUG] at death: pain={nerveSys.Pain} deathThresholdPresent={consciousness.Modifiers.ContainsKey((victim, "DeathThreshold"))}");
        });

        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems.TraumaSystem>();
        await server.WaitPost(() =>
        {
            if (sTrauma.TryGetBodyTraumas(victim, out var traumas))
                foreach (var trauma in traumas)
                    sTrauma.RemoveTrauma(trauma);

            sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(-999)), ignoreResistances: true, interruptsDoAfters: false);
            sWound.ForceHealWoundsOnWoundable(torso, out _);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var woundable = sEntMan.GetComponent<WoundableComponent>(torso);
            TestContext.Out.WriteLine($"[DEBUG] after heal: torsoIntegrity={woundable.WoundableIntegrity} mobState={(sMobState.IsDead(victim) ? "Dead" : sMobState.IsCritical(victim) ? "Critical" : "Alive")}");

            var nerveSys = sEntMan.GetComponent<NerveSystemComponent>(brain);
            Assert.That(nerveSys.Pain, Is.EqualTo(FixedPoint2.Zero),
                "The wound is fully healed - Pain should have dropped back to zero (this is the TryAddPainModifier key-mismatch fix; " +
                "OnPainRemoved's cleanup should now actually find and remove what was added).");

            Assert.That(sMobState.IsDead(victim), Is.False, "Sanity check: healing below the Dead threshold should have revived the mob.");

            var consciousness = sEntMan.GetComponent<Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components.ConsciousnessComponent>(victim);
            Assert.That(consciousness.Modifiers.ContainsKey((victim, "DeathThreshold")), Is.False,
                "The DeathThreshold modifier should have been removed on leaving Dead - this is the ConsciousnessSystem.OnMobStateChanged fix.");

            Assert.That(consciousness.RawConsciousness, Is.EqualTo(consciousness.Cap),
                "With no damage, no pain, and no DeathThreshold leak, consciousness should be back at its full cap.");
        });
    }
}

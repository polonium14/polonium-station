// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
[TestOf(typeof(Content.Shared.Medical.Healing.HealingSystem))]
public sealed class HealingRateTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> PiercingDamageType = "Piercing";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HealRateTestAttacker
  components:
  - type: Targeting

- type: entity
  id: HealRateTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable

- type: entity
  id: HealRateTestTorsoOrgan
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

- type: entity
  id: HealRateTestBrutepack
  components:
  - type: Healing
    damage:
      types:
        Piercing: -20

- type: entity
  id: HealRateTestSuture
  components:
  - type: Healing
    damage:
      types:
        Piercing: -20
    bloodlossModifier: -999
";

    private async Task<(EntityUid Attacker, EntityUid Victim, EntityUid Organ, MapCoordinates Coords, IEntityManager EntMan, DamageableSystem Damageable, WoundSystem Wound)> Setup()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("HealRateTestAttacker", coords);
            victim = sEntMan.SpawnEntity("HealRateTestVictim", coords);
            organ = sEntMan.SpawnEntity("HealRateTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        // Deal damage to the MOB, attacker's default TargetingComponent selection (Chest ->
        // Torso), letting BodyDamageBridgeSystem mirror it onto the organ and create a real
        // wound - the same path a combat hit takes. 20 Piercing in one hit crosses the bleed
        // severity threshold, so this wound starts actively bleeding (matches the "bones break
        // reliably" magnitude TraumaBoneBreakTest uses for the same reason).
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: false, origin: attacker);
        });

        await pair.RunTicksSync(5);

        return (attacker, victim, organ, coords, sEntMan, sDamageable, sWound);
    }

    private static FixedPoint2 SumWoundSeverity(IEntityManager sEntMan, WoundSystem sWound, EntityUid organ)
    {
        var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
        return sWound.GetWoundableWounds(organ, woundableComp).Aggregate(FixedPoint2.Zero, (acc, w) => acc + w.Comp.WoundSeverityPoint);
    }

    private async Task ApplyHeal(EntityUid attacker, EntityUid victim, MapCoordinates coords, string healerProto)
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();

        await server.WaitPost(() =>
        {
            var healer = sEntMan.SpawnEntity(healerProto, coords);
            var doAfterArgs = new DoAfterArgs(sEntMan, attacker, TimeSpan.FromSeconds(1), new HealingDoAfterEvent(), victim, target: victim, used: healer);
            var ev = new HealingDoAfterEvent
            {
                DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero),
            };
            sEntMan.EventBus.RaiseLocalEvent(victim, ev);
        });

        await pair.RunTicksSync(5);
    }

    /// <summary>
    /// A bleeding wound (brutepack has no bloodlossModifier, so it never stops the bleed first)
    /// must not have the mob's/organ's raw damage reduced at all - matches BrokenBoneBlocksTopicalHealing's
    /// existing coverage for the woundable-level blockers, but for the per-wound bleed blocker instead.
    /// </summary>
    [Test]
    public async Task BleedingWoundBlocksTheHealAndTheMobDoesNotMove()
    {
        var (attacker, victim, organ, coords, sEntMan, sDamageable, sWound) = await Setup();

        FixedPoint2 mobBefore = default, organBefore = default, woundBefore = default;
        await Pair.Server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            mobBefore = sDamageable.GetTotalDamage(victim);
            organBefore = sDamageable.GetTotalDamage(organ);
#pragma warning restore CS0618
            woundBefore = SumWoundSeverity(sEntMan, sWound, organ);
        });

        await ApplyHeal(attacker, victim, coords, "HealRateTestBrutepack");

        await Pair.Server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var mobAfter = sDamageable.GetTotalDamage(victim);
            var organAfter = sDamageable.GetTotalDamage(organ);
#pragma warning restore CS0618
            var woundAfter = SumWoundSeverity(sEntMan, sWound, organ);

            Assert.That(mobAfter, Is.EqualTo(mobBefore),
                "A bandage with no bloodloss modifier shouldn't be able to touch a bleeding wound's damage on the mob at all - the wound is blocked, so nothing should have healed.");
            Assert.That(organAfter, Is.EqualTo(organBefore),
                "Same as the mob check, but on the organ's own raw DamageableComponent (read by WoundSystem.Queries.cs's GetDamageableStatesOnBody for the UI doll).");
            Assert.That(woundAfter, Is.EqualTo(woundBefore),
                "The wound itself should be completely untouched while it's still bleeding.");
        });
    }

    /// <summary>
    /// Once the bleed is stopped (medicated suture has a bloodlossModifier), the same magnitude
    /// heal should land exactly - mob, organ, and wound all drop by the real absorbed amount,
    /// not more.
    /// </summary>
    [Test]
    public async Task UnblockedHealMovesMobOrganAndWoundByTheSameRealAmount()
    {
        var (attacker, victim, organ, coords, sEntMan, sDamageable, sWound) = await Setup();

        FixedPoint2 mobBefore = default, organBefore = default, woundBefore = default;
        await Pair.Server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            mobBefore = sDamageable.GetTotalDamage(victim);
            organBefore = sDamageable.GetTotalDamage(organ);
#pragma warning restore CS0618
            woundBefore = SumWoundSeverity(sEntMan, sWound, organ);
        });

        await ApplyHeal(attacker, victim, coords, "HealRateTestSuture");

        await Pair.Server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var mobAfter = sDamageable.GetTotalDamage(victim);
            var organAfter = sDamageable.GetTotalDamage(organ);
#pragma warning restore CS0618
            var woundAfter = SumWoundSeverity(sEntMan, sWound, organ);

            var mobHealed = mobBefore - mobAfter;
            var organHealed = organBefore - organAfter;
            var woundHealed = woundBefore - woundAfter;

            Assert.That(woundHealed, Is.EqualTo(FixedPoint2.New(20)), "The suture's -20 Piercing should fully heal the 20-severity wound once bleeding is stopped first.");
            Assert.That(organHealed, Is.EqualTo(woundHealed), "The organ's raw damage should drop by exactly what the wound actually absorbed.");
            Assert.That(mobHealed, Is.EqualTo(woundHealed), "The mob's raw damage should drop by exactly what the wound actually absorbed, not the nominal item amount.");
        });
    }

    [Test]
    public async Task WoundlessMobDamageIsStillHealableByATopical()
    {
        // Deliberately not the shared Setup() helper - it bakes in a targeted, wound-backed hit
        // this test needs to not exist at all, to cleanly measure wound-less healing alone.
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("HealRateTestAttacker", coords);
            victim = sEntMan.SpawnEntity("HealRateTestVictim", coords);
            organ = sEntMan.SpawnEntity("HealRateTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        // Mirrors exactly what BarotraumaSystem now does around its own TryChangeDamage calls -
        // no origin, marker present, so this never reaches the organ or creates any wound.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            sEntMan.AddComponent<Content.Shared._Shitmed.Body.SkipDamageBridgeComponent>(victim);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(15)));
            sEntMan.RemoveComponent<Content.Shared._Shitmed.Body.SkipDamageBridgeComponent>(victim);
        });

        await pair.RunTicksSync(5);

        FixedPoint2 mobBefore = default, organBefore = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            mobBefore = sDamageable.GetTotalDamage(victim);
            organBefore = sDamageable.GetTotalDamage(organ);
#pragma warning restore CS0618
            Assert.That(mobBefore, Is.EqualTo(FixedPoint2.New(15)), "Sanity check: the wound-less damage should have landed on the mob only.");
            Assert.That(organBefore, Is.EqualTo(FixedPoint2.Zero), "Sanity check: the organ should be completely untouched - no wound was ever created for this damage.");
        });

        await ApplyHeal(attacker, victim, coords, "HealRateTestSuture");

        await Pair.Server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var mobAfter = sDamageable.GetTotalDamage(victim);
            var organAfter = sDamageable.GetTotalDamage(organ);
#pragma warning restore CS0618

            Assert.That(mobBefore - mobAfter, Is.EqualTo(FixedPoint2.New(15)),
                "The suture's -20 Piercing should fully heal the mob's 15 wound-less damage, floored at what's really there rather than blocked entirely by the missing wound.");
            Assert.That(organAfter, Is.EqualTo(FixedPoint2.Zero),
                "The organ had nothing to heal and should stay untouched.");
        });
    }
}

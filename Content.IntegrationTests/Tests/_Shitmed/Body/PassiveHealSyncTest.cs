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
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// User report, right after PassiveDamageComponent was removed from species_base.yml: "now
/// there is no passive healing at all." That removal exposed a real, separate pre-existing bug
/// (already flagged, not yet fixed, at the time it was found): WoundSystem's own passive healing
/// (ProcessHealing/TryHealWoundsOnWoundable) only ever called SetWoundSeverity - it never told
/// DamageableSystem about it, so the organ's and mob's raw DamageableComponent damage (what the
/// health analyzer's total-damage readout and crit/death risk actually use) never moved from
/// natural regen, only WoundableIntegrity did. PassiveDamageComponent had been the only thing
/// incidentally making the total-damage number go down over time; removing it (correctly, to
/// match Goob) made this pre-existing gap visible for the first time. Fixed by having
/// ProcessHealing mirror the exact amount TryHealWoundsOnWoundable actually healed onto both the
/// organ's and the mob's raw DamageableComponent - same "clamp to what's real" pattern as
/// HealingSystem.OnDoAfter's item-healing fix, with a WoundSystem._suppressWoundInduction guard
/// so the organ-side mirror call doesn't re-enter OnDamageDealt and heal the same wound twice.
/// </summary>
[TestFixture]
[TestOf(typeof(WoundSystem))]
public sealed class PassiveHealSyncTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: PassiveHealSyncAttacker
  components:
  - type: Targeting

- type: entity
  id: PassiveHealSyncVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable

- type: entity
  id: PassiveHealSyncTorsoOrgan
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
";

    [Test]
    public async Task PassiveHealingMovesRawDamageByExactlyTheHealedAmountOnce()
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
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("PassiveHealSyncAttacker", coords);
            victim = sEntMan.SpawnEntity("PassiveHealSyncVictim", coords);
            torso = sEntMan.SpawnEntity("PassiveHealSyncTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);

            sEntMan.GetComponent<TargetingComponent>(attacker).Target = TargetBodyPart.Chest;
        });

        await pair.RunTicksSync(5);

        // 3 Blunt - below WoundBlunt's BleedInflicter severityThreshold (8), so it never bleeds
        // and CanHealWound is never blocked - isolates this test to the sync fix itself.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(3)), ignoreResistances: false, origin: attacker);
        });

        await pair.RunTicksSync(5);

        FixedPoint2 mobBefore = default, organBefore = default, woundBefore = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            mobBefore = sDamageable.GetTotalDamage(victim);
            organBefore = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(torso);
            woundBefore = sWound.GetWoundableWounds(torso, woundableComp).Aggregate(FixedPoint2.Zero, (acc, w) => acc + w.Comp.WoundSeverityPoint);

            Assert.That(mobBefore, Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(organBefore, Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(woundBefore, Is.EqualTo(FixedPoint2.New(3)));
        });

        // Past the 2s MinimumTimeBeforeHeal gate - one passive heal tick should fire
        // (healAbility overridden to 5, well above the 3 severity present, so it fully heals in
        // a single tick - makes "did it double" trivially checkable: healed-to-zero-once is
        // correct, going negative or getting force-clamped-at-zero either way would mask a
        // double-heal, so the real signal is mob/organ/wound all landing on exactly 0, in sync).
        await pair.RunSeconds(2.5f);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var mobAfter = sDamageable.GetTotalDamage(victim);
            var organAfter = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(torso);
            var woundAfter = sWound.GetWoundableWounds(torso, woundableComp).Aggregate(FixedPoint2.Zero, (acc, w) => acc + w.Comp.WoundSeverityPoint);

            Assert.That(woundAfter, Is.EqualTo(FixedPoint2.Zero), "The 3-severity wound should be fully healed by a single 5-severity passive tick.");
            Assert.That(organAfter, Is.EqualTo(woundAfter), "The organ's raw damage should track the wound severity exactly - this is the fix itself.");
            Assert.That(mobAfter, Is.EqualTo(woundAfter), "The mob's raw damage (what the health analyzer reads) should also track the wound severity exactly.");
        });
    }

    /// <summary>
    /// Same fix, but with a wound severity larger than one tick's heal amount - the case where a
    /// double-heal bug would actually be visible (a single-tick full-heal can't distinguish
    /// "healed exactly once" from "healed twice and got clamped at zero either way"). Two
    /// consecutive ticks should each move mob/organ/wound by exactly HealAbility, never 2x.
    /// </summary>
    [Test]
    public async Task PassiveHealingDoesNotDoubleAcrossMultipleTicks()
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
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("PassiveHealSyncAttacker", coords);
            victim = sEntMan.SpawnEntity("PassiveHealSyncVictim", coords);
            torso = sEntMan.SpawnEntity("PassiveHealSyncTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);

            sEntMan.GetComponent<TargetingComponent>(attacker).Target = TargetBodyPart.Chest;
        });

        await pair.RunTicksSync(5);

        // 7 Blunt - stays under WoundBlunt's BleedInflicter severityThreshold (8), so it never
        // bleeds and CanHealWound is never blocked. healAbility=5/tick means two ticks are
        // needed (5, then the remaining 2), giving room to observe per-tick deltas instead of a
        // single full clear.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(7)), ignoreResistances: false, origin: attacker);
        });

        await pair.RunTicksSync(5);

        FixedPoint2 GetWoundTotal()
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(torso);
            return sWound.GetWoundableWounds(torso, woundableComp).Aggregate(FixedPoint2.Zero, (acc, w) => acc + w.Comp.WoundSeverityPoint);
        }

        FixedPoint2 mobBefore = default, organBefore = default, woundBefore = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            mobBefore = sDamageable.GetTotalDamage(victim);
            organBefore = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618
            woundBefore = GetWoundTotal();
        });

        // First tick.
        await pair.RunSeconds(2.5f);

        FixedPoint2 mobAfterOne = default, organAfterOne = default, woundAfterOne = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            mobAfterOne = sDamageable.GetTotalDamage(victim);
            organAfterOne = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618
            woundAfterOne = GetWoundTotal();

            var woundHealed = woundBefore - woundAfterOne;
            Assert.That(woundHealed, Is.EqualTo(FixedPoint2.New(5)), "One tick at healAbility=5 should heal exactly 5 severity.");
            Assert.That(organBefore - organAfterOne, Is.EqualTo(woundHealed), "Organ raw damage should drop by exactly the wound-healed amount, not double it.");
            Assert.That(mobBefore - mobAfterOne, Is.EqualTo(woundHealed), "Mob raw damage should drop by exactly the wound-healed amount, not double it.");
        });

        // Second tick.
        await pair.RunSeconds(2.5f);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var mobAfterTwo = sDamageable.GetTotalDamage(victim);
            var organAfterTwo = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618
            var woundAfterTwo = GetWoundTotal();

            var woundHealed = woundAfterOne - woundAfterTwo;
            Assert.That(woundHealed, Is.EqualTo(FixedPoint2.New(2)), "Second tick should heal only the 2 remaining severity, not another 5 (and definitely not double either tick's amount).");
            Assert.That(organAfterOne - organAfterTwo, Is.EqualTo(woundHealed), "Organ raw damage delta should still match wound-healed delta on the second tick.");
            Assert.That(mobAfterOne - mobAfterTwo, Is.EqualTo(woundHealed), "Mob raw damage delta should still match wound-healed delta on the second tick.");
        });
    }
}

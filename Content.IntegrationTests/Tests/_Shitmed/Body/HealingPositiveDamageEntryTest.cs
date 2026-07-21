using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Medical.Healing;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// Code-review finding: HealingSystem's organ-heal split loop (the hasOrgan branch) skipped any
/// DamageSpecifier entry whose value was &gt;= 0 (a positive/side-effect damage type mixed into
/// an otherwise-healing item, e.g. "heals brute but deals a bit of Poison") entirely - both
/// actualOrganHeal and mobOnlyHeal started as full copies of the scaled DamageSpecifier, and the
/// skip left that entry untouched in BOTH, so it got applied once to the organ (which then
/// auto-mirrors to the mob via BodyDamageBridgeSystem) AND a second time directly to the mob,
/// double-counting it. No shipped healing item currently has a positive entry, so this is
/// exercised directly with a synthetic one rather than real content. Fixed by zeroing the entry
/// out of mobOnlyHeal, matching the same "the organ side already reaches the mob on its own"
/// reasoning already used for the negative branch just above it.
/// </summary>
[TestFixture]
[TestOf(typeof(HealingSystem))]
public sealed class HealingPositiveDamageEntryTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HealingPositiveEntryTestAttacker
  components:
  - type: Targeting

- type: entity
  id: HealingPositiveEntryTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable

- type: entity
  id: HealingPositiveEntryTestTorsoOrgan
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
  id: HealingPositiveEntryTestItem
  components:
  - type: Healing
    damage:
      types:
        Poison: 5
";

    [Test]
    public async Task PositiveDamageEntryOnlyAppliesOnce()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("HealingPositiveEntryTestAttacker", coords);
            victim = sEntMan.SpawnEntity("HealingPositiveEntryTestVictim", coords);
            organ = sEntMan.SpawnEntity("HealingPositiveEntryTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            Assert.That(sDamageable.GetTotalDamage(victim), Is.EqualTo(FixedPoint2.Zero), "Sanity check: victim should start with zero damage.");
#pragma warning restore CS0618
        });

        await server.WaitPost(() =>
        {
            var item = sEntMan.SpawnEntity("HealingPositiveEntryTestItem", coords);
            var doAfterArgs = new DoAfterArgs(sEntMan, attacker, TimeSpan.FromSeconds(1), new HealingDoAfterEvent(), victim, target: victim, used: item);
            var ev = new HealingDoAfterEvent
            {
                DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero),
            };
            sEntMan.EventBus.RaiseLocalEvent(victim, ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var organDamage = sDamageable.GetTotalDamage(organ);
            var mobDamage = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618

            Assert.That(organDamage, Is.EqualTo(FixedPoint2.New(5)),
                "The organ should have taken the 5 Poison damage once.");
            Assert.That(mobDamage, Is.EqualTo(FixedPoint2.New(5)),
                "The mob's total should also read 5 (auto-mirrored from the organ), not 10 - the old bug applied the positive entry to both the organ AND directly to the mob.");
        });
    }
}

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// Every point of damage the mob takes lies on a body part that is present: a limb takes damage up to its integrity
/// cap and passes the rest to its parent, a hit on a missing limb lands on the nearest part still there, and damage
/// spread over the whole body adds up to what the mob took.
/// </summary>
[TestFixture]
public sealed class LimbDamageCapTest : GameTest
{
    private DamageableSystem Damageable => SEntMan.System<DamageableSystem>();

    private EntityUid Part(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    private FixedPoint2 PartsTotal(EntityUid body)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Where(o => SEntMan.HasComponent<WoundableComponent>(o))
            .Aggregate(FixedPoint2.Zero, (sum, o) => sum + Damageable.GetTotalDamage(o));
    }

    private DamageSpecifier Blunt(float amount)
    {
        return new DamageSpecifier(Server.ProtoMan.Index<DamageTypePrototype>("Blunt"), amount);
    }

    private EntityUid Attacker(EntityCoordinates coords, TargetBodyPart target)
    {
        var attacker = SEntMan.SpawnAtPosition("MobHuman", coords);
        SEntMan.GetComponent<TargetingComponent>(attacker).Target = target;
        return attacker;
    }

    [Test]
    public async Task LimbPassesDamageOverItsCapToTheTorso()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var leg = Part(body, "LegLeft");
            var cap = SEntMan.GetComponent<WoundableComponent>(leg).IntegrityCap;

            // Lying down, so the hit lands where it is aimed.
            var attacker = Attacker(map.GridCoords, TargetBodyPart.LeftLeg);
            SEntMan.System<Content.Shared.Standing.StandingStateSystem>().Down(body);
            Damageable.TryChangeDamage(body, Blunt(150), ignoreResistances: true, origin: attacker);

            Assert.Multiple(() =>
            {
                Assert.That(Damageable.GetTotalDamage(leg), Is.EqualTo(cap), "The leg takes damage up to its cap.");
                Assert.That(Damageable.GetTotalDamage(Part(body, "Torso")), Is.EqualTo(FixedPoint2.New(150) - cap),
                    "The rest goes to the torso.");
                Assert.That(PartsTotal(body), Is.EqualTo(Damageable.GetTotalDamage(body)), "The mob is the sum of its parts.");
            });
        });
    }

    [Test]
    public async Task HitOnAMissingLimbLandsOnTheTorso()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(SEntMan.System<BodyDebugSystem>().TryAmputate(body, Part(body, "ArmLeft")));
            var attacker = Attacker(map.GridCoords, TargetBodyPart.LeftArm);

            for (var i = 0; i < 10; i++)
            {
                Damageable.TryChangeDamage(body, Blunt(5), ignoreResistances: true, origin: attacker);
            }

            Assert.Multiple(() =>
            {
                Assert.That(Damageable.GetTotalDamage(body), Is.EqualTo(FixedPoint2.New(50)));
                Assert.That(PartsTotal(body), Is.EqualTo(FixedPoint2.New(50)), "No hit is left on the mob alone.");
            });
        });
    }

    [Test]
    public async Task DamageWithoutOriginAddsUpOverTheLimbs()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Damageable.TryChangeDamage(body, new DamageSpecifier(Server.ProtoMan.Index<DamageTypePrototype>("Heat"), 10),
                ignoreResistances: true);

            Assert.Multiple(() =>
            {
                Assert.That(Damageable.GetTotalDamage(body), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(PartsTotal(body), Is.EqualTo(FixedPoint2.New(10)), "The limbs together take what the mob took.");
                Assert.That(Damageable.GetTotalDamage(Part(body, "Torso")), Is.GreaterThan(Damageable.GetTotalDamage(Part(body, "LegLeft"))),
                    "The torso takes the biggest share.");
            });
        });
    }
}

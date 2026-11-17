using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// Severing limbs: a blade only takes off a limb that is already badly hurt, a head only comes off
/// a body that is down, and a strong explosion tears off one arm or leg.
/// </summary>
[TestFixture]
public sealed class DismembermentTest : GameTest
{
    // CVars are changed, so the pair is not reused.
    public override PoolSettings PoolSettings => new() { Dirty = true };

    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";

    private EntityUid Part(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .SingleOrDefault(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    /// <summary>
    /// A lying victim, so the hit lands where it is aimed, with one part brought down to the given integrity.
    /// </summary>
    private EntityUid Victim(EntityCoordinates coords, string category, float integrity)
    {
        var body = SEntMan.SpawnAtPosition("MobHuman", coords);
        SEntMan.System<StandingStateSystem>().Down(body);
        Assert.That(SEntMan.System<BodyDebugSystem>().TrySetLimbIntegrity(body, Part(body, category), integrity));
        return body;
    }

    private void Cut(EntityUid body, EntityCoordinates coords, TargetBodyPart target)
    {
        var attacker = SEntMan.SpawnAtPosition("MobHuman", coords);
        SEntMan.GetComponent<TargetingComponent>(attacker).Target = target;
        SEntMan.System<DamageableSystem>().TryChangeDamage(body, new DamageSpecifier(Server.ProtoMan.Index(Slash), 15),
            ignoreResistances: true, origin: attacker);
    }

    [Test]
    public async Task BladeSeversOnlyABadlyHurtLimb()
    {
        // Any chance above zero becomes certain, so the test only sees whether the roll is allowed.
        Server.CfgMan.SetCVar(CCVars.DismembermentChanceMultiplier, 1000f);
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var healthy = Victim(map.GridCoords, "ArmLeft", 80);
            var hurt = Victim(map.GridCoords, "ArmLeft", 30);
            Cut(healthy, map.GridCoords, TargetBodyPart.LeftArm);
            Cut(hurt, map.GridCoords, TargetBodyPart.LeftArm);

            Assert.Multiple(() =>
            {
                Assert.That(Part(healthy, "ArmLeft").IsValid(), "A healthy arm stays on.");
                Assert.That(Part(hurt, "ArmLeft").IsValid(), Is.False, "A badly hurt arm comes off.");
                Assert.That(SEntMan.System<TraumaSystem>().TryGetWoundableTrauma(Part(hurt, "Torso"), out _, TraumaType.Dismemberment),
                    "The torso keeps the stump.");
                // The arm had 50 damage and took 15 more with the cut; half of that stays behind on the torso.
                Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(Part(hurt, "Torso")).Float(), Is.InRange(25f, 33f),
                    "The torso takes half of the lost arm's damage.");
                Assert.That(SEntMan.GetComponent<BloodstreamComponent>(hurt).BleedAmount, Is.GreaterThan(0f), "The stump bleeds.");
            });
        });
    }

    [Test]
    public async Task HeadComesOffOnlyABodyThatIsDown()
    {
        Server.CfgMan.SetCVar(CCVars.DismembermentChanceMultiplier, 1000f);
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            // 65 damage on the head, so the cut does not bring the living victim to critical condition.
            var alive = Victim(map.GridCoords, "Head", 60);
            var dead = Victim(map.GridCoords, "Head", 60);
            SEntMan.System<MobStateSystem>().ChangeMobState(dead, MobState.Dead);
            Assert.That(SEntMan.System<MobStateSystem>().IsAlive(alive), "The wounded head alone does not put the victim down.");

            Cut(alive, map.GridCoords, TargetBodyPart.Head);
            Cut(dead, map.GridCoords, TargetBodyPart.Head);

            Assert.Multiple(() =>
            {
                Assert.That(Part(alive, "Head").IsValid(), "Nobody is beheaded while still up.");
                Assert.That(Part(dead, "Head").IsValid(), Is.False, "A corpse can be beheaded.");
            });
        });
    }

    [Test]
    public async Task BodyWithoutABrainStillLosesLimbs()
    {
        Server.CfgMan.SetCVar(CCVars.DismembermentChanceMultiplier, 1000f);
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var body = Victim(map.GridCoords, "ArmLeft", 30);
            var organs = SEntMan.GetComponent<BodyComponent>(body).Organs!;
            SEntMan.System<Robust.Shared.Containers.SharedContainerSystem>().Remove(Part(body, "Brain"), organs);

            Cut(body, map.GridCoords, TargetBodyPart.LeftArm);
            Assert.That(Part(body, "ArmLeft").IsValid(), Is.False, "No brain, no nerves, but the arm still comes off.");
        });
    }

    [Test]
    public async Task BrokenBoneOpensTheWay()
    {
        Server.CfgMan.SetCVar(CCVars.DismembermentChanceMultiplier, 1000f);
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            // Half its integrity left: not badly hurt yet, so only the bone decides.
            var whole = Victim(map.GridCoords, "ArmLeft", 50);
            var broken = Victim(map.GridCoords, "ArmLeft", 50);
            var arm = SEntMan.GetComponent<WoundableComponent>(Part(broken, "ArmLeft"));
            Assert.That(SEntMan.System<TraumaSystem>().SetBoneIntegrity(arm.Bone!.ContainedEntities.Single(), 0));

            Cut(whole, map.GridCoords, TargetBodyPart.LeftArm);
            Cut(broken, map.GridCoords, TargetBodyPart.LeftArm);

            Assert.Multiple(() =>
            {
                Assert.That(Part(whole, "ArmLeft").IsValid(), "A whole bone holds the arm.");
                Assert.That(Part(broken, "ArmLeft").IsValid(), Is.False, "A broken bone lets the blade through.");
            });
        });
    }

    [Test]
    public async Task ExplosionThatGibsTheBodyDoesNotBreak()
    {
        Server.CfgMan.SetCVar(CCVars.DismembermentExplosionChancePerDamage, 1f);
        var map = await Pair.CreateTestMap();
        EntityUid body = default;

        await Server.WaitPost(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            // A syndicate minibomb under the feet.
            SEntMan.System<ExplosionSystem>().QueueExplosion(
                SEntMan.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords), "TransparentMinibomb", 150f, 75f, 100f, null, addLog: false);
        });

        await Pair.RunSeconds(3);

        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(body), "The body is gibbed."));
    }

    // A grenade under the feet deals about 100 blunt and piercing damage.
    [TestCase(40f, 1f, true)]
    [TestCase(40f, 0f, false)]
    [TestCase(95f, 1f, true)]
    [TestCase(105f, 1f, false)]
    public async Task ExplosionTearsOffOneLimb(float threshold, float chancePerDamage, bool severed)
    {
        Server.CfgMan.SetCVar(CCVars.DismembermentExplosionThreshold, threshold);
        Server.CfgMan.SetCVar(CCVars.DismembermentExplosionChancePerDamage, chancePerDamage);
        var map = await Pair.CreateTestMap();
        EntityUid body = default;

        await Server.WaitPost(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            // A standard explosive grenade under the feet.
            SEntMan.System<ExplosionSystem>().QueueExplosion(
                SEntMan.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords), "Transparent", 120f, 3f, 10f, null, addLog: false);
        });

        await Pair.RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            string[] limbs = ["ArmLeft", "ArmRight", "LegLeft", "LegRight"];
            var left = limbs.Count(limb => Part(body, limb).IsValid());

            Assert.Multiple(() =>
            {
                Assert.That(left, Is.EqualTo(severed ? 3 : 4));
                Assert.That(Part(body, "Head").IsValid(), "The head stays.");
                Assert.That(SEntMan.System<TraumaSystem>().TryGetWoundableTrauma(Part(body, "Torso"), out _, TraumaType.Dismemberment),
                    Is.EqualTo(severed));
            });
        });
    }
}

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
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
/// Severing limbs: a blade only takes off a limb that is already badly hurt, and a head only comes off
/// a body that is down.
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
}

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// The body debug window's server actions on body parts: integrity, bone, wounds, amputation and restoring.
/// </summary>
[TestFixture]
public sealed class BodyDebugLimbTest : GameTest
{
    private BodyDebugSystem Debug => SEntMan.System<BodyDebugSystem>();

    private EntityUid? Find(EntityUid body, string category)
    {
        foreach (var organ in SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities)
        {
            if (SEntMan.GetComponent<OrganComponent>(organ).Category == category)
                return organ;
        }

        return null;
    }

    [Test]
    public async Task IntegrityWoundsAndBone()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var arm = Find(body, "ArmLeft")!.Value;
            var woundable = SEntMan.GetComponent<WoundableComponent>(arm);
            var wounds = SEntMan.System<WoundSystem>();
            var traumas = SEntMan.System<TraumaSystem>();
            var cap = woundable.IntegrityCap;

            Assert.That(Debug.TrySetLimbIntegrity(body, arm, 50));
            Assert.That(woundable.WoundableIntegrity, Is.EqualTo(cap / 2), "Slider down.");

            Assert.That(Debug.TrySetLimbIntegrity(body, arm, 80));
            Assert.That(woundable.WoundableIntegrity, Is.EqualTo(cap * 0.8f), "Slider up.");

            var before = wounds.GetWoundableWounds(arm).Count();
            Assert.That(Debug.TryAddWound(body, arm, "Slash", 10));
            Assert.That(wounds.GetWoundableWounds(arm).Count(), Is.EqualTo(before + 1));
            Assert.That(Debug.TryAddWound(body, arm, "NotADamageType", 10), Is.False);

            Assert.That(Debug.TryHealLimb(body, arm));
            Assert.That(woundable.WoundableIntegrity, Is.EqualTo(cap), "Heal wounds.");

            var bone = SEntMan.GetComponent<BoneComponent>(woundable.Bone!.ContainedEntities[0]);
            Assert.That(Debug.TrySetBoneIntegrity(body, arm, 0));
            Assert.Multiple(() =>
            {
                Assert.That(bone.BoneSeverity, Is.EqualTo(BoneSeverity.Broken));
                Assert.That(traumas.HasWoundableTrauma(arm, TraumaType.BoneDamage), "A broken bone has a trauma surgery can mend.");
            });

            Assert.That(Debug.TrySetBoneIntegrity(body, arm, 100));
            Assert.Multiple(() =>
            {
                Assert.That(bone.BoneIntegrity, Is.EqualTo(bone.IntegrityCap));
                Assert.That(bone.BoneSeverity, Is.EqualTo(BoneSeverity.Normal));
                Assert.That(traumas.HasWoundableTrauma(arm, TraumaType.BoneDamage), Is.False);
            });
        });
    }

    [Test]
    public async Task AmputateAndRestore()
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var torso = Find(body, "Torso")!.Value;
            Assert.That(Debug.TryAmputate(body, torso), Is.False, "The torso stays.");
            Assert.That(Debug.TryAmputate(body, Find(body, "ArmLeft")!.Value));
        });

        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Find(body, "ArmLeft"), Is.Null);
            Assert.That(Find(body, "HandLeft"), Is.Null, "The hand goes with the arm.");
            var missing = Debug.GetLimbs(body).Where(row => row.Limb == null).Select(row => row.Category).ToList();
            Assert.That(missing, Is.EquivalentTo(new[] { "ArmLeft", "HandLeft" }));

            Assert.That(Debug.TryPutBack(body, "ArmLeft"));
            var torso = Find(body, "Torso")!.Value;
            var arm = Find(body, "ArmLeft");
            var hand = Find(body, "HandLeft");
            Assert.Multiple(() =>
            {
                Assert.That(arm, Is.Not.Null);
                Assert.That(hand, Is.Not.Null, "The hand comes back with the arm.");
                Assert.That(SEntMan.GetComponent<ChildOrganComponent>(arm!.Value).Parent, Is.EqualTo(torso));
                Assert.That(SEntMan.GetComponent<ChildOrganComponent>(hand!.Value).Parent, Is.EqualTo(arm));
                Assert.That(SEntMan.System<TraumaSystem>().HasWoundableTrauma(torso, TraumaType.Dismemberment), Is.False);
                Assert.That(Debug.GetLimbs(body).All(row => row.Limb != null));
            });
        });

        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Debug.TryAmputate(body, Find(body, "Head")!.Value));
        });

        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Find(body, "Head"), Is.Null);
            Assert.That(Find(body, "Eyes"), Is.Null, "Organs in the head leave with it.");
            Assert.That(Debug.TryPutBack(body, "Head"));
            Assert.Multiple(() =>
            {
                Assert.That(Find(body, "Head"), Is.Not.Null);
                Assert.That(Find(body, "Eyes"), Is.Not.Null);
                Assert.That(Find(body, "Brain"), Is.Not.Null);
            });
        });

        await Pair.RunTicksSync(30);
    }
}

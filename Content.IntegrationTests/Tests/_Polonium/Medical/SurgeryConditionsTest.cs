using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.IntegrationTests.Tests._Shitmed.Surgery;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Server._Polonium.Body;
using Content.Shared._Polonium.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

/// <summary>
/// Surgery in poor conditions: improvised tools are slow, a step that takes too long can fail,
/// and a patient who feels nothing is operated on faster.
/// </summary>
[TestFixture]
public sealed class SurgeryConditionsTest : GameTest
{
    // A CVar is changed, so the pair is not reused.
    public override PoolSettings PoolSettings => new() { Dirty = true };

    private static readonly EntProtoId Knife = "KitchenKnife";
    private static readonly EntProtoId Shard = "ShardGlass";
    private const string Surgery = "SurgeryOpenIncision";
    private const string Step = "SurgeryStepOpenIncisionScalpel";

    private EntityUid Torso(EntityUid body)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == "Torso");
    }

    /// <summary>
    /// A surgeon whose bare hand is a scalpel of the given speed, so the test needs no held item.
    /// </summary>
    private EntityUid Surgeon(EntityUid at, float scalpelSpeed)
    {
        var user = SEntMan.SpawnAtPosition(null, SEntMan.GetComponent<TransformComponent>(at).Coordinates);
        SEntMan.AddComponent<HandsComponent>(user);
        SEntMan.AddComponent<DoAfterComponent>(user);
        SEntMan.AddComponent<TestListenerComponent>(user);
        SEntMan.AddComponent<ScalpelComponent>(user).Speed = scalpelSpeed;
        SEntMan.System<SharedHandsSystem>().AddHand(user, "right", HandLocation.Right);
        return user;
    }

    [TestCase(1f, false)]
    [TestCase(0.1f, true)]
    public async Task HopelessToolBotchesTheStep(float scalpelSpeed, bool botched)
    {
        // Without the cap, so the hopeless tool fails for certain.
        Server.CfgMan.SetCVar(CCVars.SurgeryBotchMaxChance, 1f);
        var map = await Pair.CreateTestMap();
        EntityUid user = default, body = default, torso = default;
        var damageable = SEntMan.System<DamageableSystem>();

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            torso = Torso(body);
            SEntMan.GetComponent<SurgeryTargetComponent>(body).SepsisImmune = true;
            SEntMan.System<StandingStateSystem>().Down(body);
            user = Surgeon(body, scalpelSpeed);
            Assert.That(SEntMan.System<SurgerySystem>().TryDoSurgeryStep(body, torso, user, Surgery, Step, out var error), error.ToString());
        });

        await Pair.RunSeconds(25);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<SurgeryTreatmentCompletionTest.FailureListenerSystem>().Count(user), Is.EqualTo(botched ? 1 : 0));
                Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(torso), Is.EqualTo(!botched), "A botched step stays undone.");
                if (botched)
                    Assert.That(damageable.GetTotalDamage(body), Is.GreaterThan(FixedPoint2.Zero), "A botched step cuts the patient.");
            });
        });
    }

    [Test]
    public async Task KitchenKnifeInHandOpensAnIncision()
    {
        // No failure roll, so the outcome is certain.
        Server.CfgMan.SetCVar(CCVars.SurgeryBotchChancePerSecond, 0f);
        var map = await Pair.CreateTestMap();
        EntityUid torso = default;

        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            torso = Torso(body);
            SEntMan.GetComponent<SurgeryTargetComponent>(body).SepsisImmune = true;
            SEntMan.System<StandingStateSystem>().Down(body);

            var surgeon = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var knife = SEntMan.SpawnAtPosition(Knife, map.GridCoords);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(surgeon, knife));
            Assert.That(SEntMan.System<SurgerySystem>().TryDoSurgeryStep(body, torso, surgeon, Surgery, Step, out var error), error.ToString());
        });

        await Pair.RunSeconds(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(torso));
        });
    }

    [Test]
    public async Task RepairSurgeryRunsThroughAnOpenedChest()
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default, torso = default, user = default, liver = default;
        var surgery = SEntMan.System<SurgerySystem>();

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            torso = Torso(body);
            SEntMan.GetComponent<SurgeryTargetComponent>(body).SepsisImmune = true;
            SEntMan.System<StandingStateSystem>().Down(body);
            user = Surgeon(body, 1f);
            SEntMan.AddComponent<RetractorComponent>(user);
            SEntMan.AddComponent<BoneSawComponent>(user);
            SEntMan.AddComponent<HemostatComponent>(user);

            liver = SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
                .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == "Liver");
            var integrity = SEntMan.GetComponent<OrganIntegrityComponent>(liver);
            SEntMan.System<OrganHealingSystem>().DamageOrgan((liver, integrity), integrity.IntegrityCap * 0.9f);

            // The heart is damaged too, but the surgeon picks the liver.
            var heart = SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
                .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == "Heart");
            var heartIntegrity = SEntMan.GetComponent<OrganIntegrityComponent>(heart);
            SEntMan.System<OrganHealingSystem>().DamageOrgan((heart, heartIntegrity), heartIntegrity.IntegrityCap * 0.9f);
        });

        (string, string)[] steps =
        [
            (Surgery, Step),
            (Surgery, "SurgeryStepClampIncision"),
            (Surgery, "SurgeryStepRetractSkin"),
            ("SurgeryOpenRibcage", "SurgeryStepSawBones"),
            ("SurgeryOpenRibcage", "SurgeryStepPriseOpenBones"),
            ("SurgeryRepairLiver", "SurgeryStepRepairOrgan"),
        ];

        foreach (var (procedure, step) in steps)
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(surgery.TryDoSurgeryStep(body, torso, user, procedure, step, out var error), $"{procedure}/{step}: {error}");
            });
            await Pair.RunSeconds(12);
        }

        await Server.WaitAssertion(() =>
        {
            var integrity = SEntMan.GetComponent<OrganIntegrityComponent>(liver);
            Assert.Multiple(() =>
            {
                Assert.That(integrity.OrganIntegrity, Is.EqualTo(integrity.IntegrityCap * 0.6f));
                Assert.That(SEntMan.HasComponent<OrganRepairedComponent>(liver));
                var heart = SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
                    .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == "Heart");
                Assert.That(SEntMan.HasComponent<OrganRepairedComponent>(heart), Is.False, "Only the chosen organ is repaired.");
                Assert.That(surgery.TryDoSurgeryStep(body, torso, user, "SurgeryRepairLiver", "SurgeryStepRepairOrgan", out _), Is.False,
                    "Nothing is left to repair.");
            });
        });
    }

    /// <summary>
    /// A sawed-open chest can be closed without bone gel: the incision is gone, the ribs stay broken.
    /// </summary>
    [Test]
    public async Task SawedChestClosesWithoutBoneGel()
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default, torso = default, user = default;
        var surgery = SEntMan.System<SurgerySystem>();

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            torso = Torso(body);
            SEntMan.GetComponent<SurgeryTargetComponent>(body).SepsisImmune = true;
            SEntMan.System<StandingStateSystem>().Down(body);
            user = Surgeon(body, 1f);
            SEntMan.AddComponent<RetractorComponent>(user);
            SEntMan.AddComponent<CauteryComponent>(user);

            SEntMan.AddComponent<IncisionOpenComponent>(torso);
            SEntMan.AddComponent<IncisionClampedComponent>(torso);
            SEntMan.AddComponent<SkinRetractedComponent>(torso);
            SEntMan.AddComponent<BonesSawedComponent>(torso);
            SEntMan.AddComponent<BonesOpenComponent>(torso);
        });

        foreach (var step in new[] { "SurgeryStepCloseBones", "SurgeryStepCloseIncisionField" })
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(surgery.TryDoSurgeryStep(body, torso, user, "SurgeryCloseIncisionField", step, out var error), $"{step}: {error}");
            });
            await Pair.RunSeconds(8);
        }

        await Server.WaitAssertion(() =>
        {
            var bone = SEntMan.GetComponent<WoundableComponent>(torso).Bone!.ContainedEntities.Single();
            Assert.Multiple(() =>
            {
                Assert.That(surgery.HasUnfinishedSurgerySteps(torso), Is.False, "Nothing is left open.");
                Assert.That(SEntMan.GetComponent<BoneComponent>(bone).BoneSeverity, Is.EqualTo(BoneSeverity.Broken),
                    "The ribs were never sealed.");
            });
        });
    }

    [Test]
    public async Task ChanceGrowsWithSlownessAndPainkillerLowersIt()
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var surgery = SEntMan.System<SurgerySystem>();
            var step = surgery.GetSingleton(Step)!.Value;
            var user = Surgeon(body, 1f);
            var knife = Server.ProtoMan.Index(Knife).Components["Scalpel"].Component as ScalpelComponent;
            var shard = Server.ProtoMan.Index(Shard).Components["Scalpel"].Component as ScalpelComponent;

            Assert.Multiple(() =>
            {
                Assert.That(surgery.BotchChance(step, user, body, 1f), Is.Zero, "A real scalpel never fails, even on the floor.");
                var clown = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
                Assert.That(SEntMan.System<StatusEffectsSystem>().TrySetStatusEffectDuration(clown, "StatusEffectClumsyClown"));
                Assert.That(surgery.BotchChance(step, clown, body, 1f), Is.EqualTo(0.5f), "A clumsy surgeon fails with any tool.");

                foreach (var effect in new[] { "StatusEffectDrowsiness", "StatusEffectSeeingRainbow" })
                {
                    var addict = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
                    Assert.That(SEntMan.System<StatusEffectsSystem>().TrySetStatusEffectDuration(addict, effect));
                    Assert.That(surgery.BotchChance(step, addict, body, 1f), Is.EqualTo(0.25f), effect);
                }

                var shaking = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
                SEntMan.AddComponent<JitteringComponent>(shaking);
                Assert.That(surgery.BotchChance(step, shaking, body, 1f), Is.EqualTo(0.25f), "Shaking hands.");
                Assert.That(surgery.BotchChance(step, user, body, knife!.Speed), Is.InRange(0.01f, 0.5f));
                Assert.That(surgery.BotchChance(step, user, body, shard!.Speed), Is.GreaterThan(surgery.BotchChance(step, user, body, knife.Speed)));
            });

            Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(body, new Solution("Morphine", 5)));
        });

        await Pair.RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            var surgery = SEntMan.System<SurgerySystem>();
            var step = surgery.GetSingleton(Step)!.Value;
            var knife = Server.ProtoMan.Index(Knife).Components["Scalpel"].Component as ScalpelComponent;

            Assert.Multiple(() =>
            {
                Assert.That(surgery.FeelsNoPain(body), "Morphine numbs the patient.");
                Assert.That(surgery.BotchChance(step, body, body, knife!.Speed), Is.Zero, "A numb patient makes a knife safe.");
            });
        });
    }
}

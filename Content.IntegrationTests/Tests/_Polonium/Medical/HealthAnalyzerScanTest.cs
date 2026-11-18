using Content.Server._Polonium.Medical.HealthAnalyzer;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Server.Medical.HealthAnalyzer;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Polonium.Xenonids;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

/// <summary>
/// The one-view health analyzer gets everything about a patient in a single message.
/// </summary>
[TestFixture]
public sealed class HealthAnalyzerScanTest : GameTest
{
    private BodyDebugSystem Debug => SEntMan.System<BodyDebugSystem>();
    private PatientScanSystem Analyzer => SEntMan.System<PatientScanSystem>();

    private EntityUid Part(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    [Test]
    public async Task ScanCarriesTheWholePatient()
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);

            Assert.That(Debug.TryKillOrgan(body, Part(body, "LungLeft")));
            var heart = Part(body, "Heart");
            Assert.That(Debug.TrySetOrganIntegrity(body, heart, SEntMan.GetComponent<OrganIntegrityComponent>(heart).IntegrityCap * 0.2f));
            Assert.That(Debug.TrySetFailureStage(body, heart, 2));
            Assert.That(Debug.TrySetBoneIntegrity(body, Part(body, "Torso"), FixedPoint2.Zero));
            Assert.That(Debug.TryAddWound(body, Part(body, "ArmLeft"), "Slash", 30));
            Assert.That(Debug.TryRemoveOrgan(body, Part(body, "Spleen")));
        });

        // The cut needs a moment to start bleeding.
        await Pair.RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            var scan = Analyzer.BuildScan(body, true);
            Assert.Multiple(() =>
            {
                Assert.That(scan.Target, Is.EqualTo(SEntMan.GetNetEntity(body)));
                Assert.That(scan.ScanActive, Is.True);
                Assert.That(scan.Organs.Single(o => o.Category == "LungLeft").Dead);
                Assert.That(scan.Organs.Single(o => o.Category == "Heart").FailureStage, Is.EqualTo(2));
                Assert.That(scan.Organs.Single(o => o.Category == "Spleen").Missing);
                Assert.That(scan.Limbs[TargetBodyPart.Chest].Bone, Is.EqualTo(BoneSeverity.Broken));
                Assert.That(scan.Limbs[TargetBodyPart.LeftArm].Bleeding);
                Assert.That(scan.Limbs[TargetBodyPart.LeftArm].IntegrityCap, Is.GreaterThan(FixedPoint2.Zero),
                    "The limb's cap is the damage bar's scale when the limb is selected.");
                Assert.That(scan.Limbs[TargetBodyPart.LeftArm].Damage.Values.Aggregate(FixedPoint2.Zero, (a, b) => a + b),
                    Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(scan.Damage.Values.Aggregate(FixedPoint2.Zero, (a, b) => a + b),
                    Is.EqualTo(SEntMan.System<DamageableSystem>().GetTotalDamage(body)), "Whole-body damage is the mob's own total.");
                Assert.That(scan.CritThreshold, Is.EqualTo(FixedPoint2.New(100)));
                Assert.That(scan.DeadThreshold, Is.EqualTo(FixedPoint2.New(200)));
                Assert.That(scan.BloodlossThreshold, Is.GreaterThan(0f));
                Assert.That(scan.NormalBloodVolume, Is.GreaterThan(FixedPoint2.Zero), "Blood is shown in units.");
                Assert.That(scan.BloodVolume, Is.LessThanOrEqualTo(scan.NormalBloodVolume * 1.2f));
                Assert.That(scan.HeatDamageThreshold, Is.GreaterThan(scan.ColdDamageThreshold));
                Assert.That(scan.Solutions, Is.Not.Empty, "The blood is always there.");
            });
        });
    }

    [Test]
    public async Task ScanFitsEveryBody()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Analyzer.BuildScan(null, null).Target, Is.Null, "No patient sends an empty scan.");

            var diona = Analyzer.BuildScan(SEntMan.SpawnAtPosition("MobDiona", map.GridCoords), true);
            Assert.Multiple(() =>
            {
                Assert.That(diona.Organs.Any(o => o.Category == "Lungs"), "A diona has a single lung.");
                Assert.That(diona.Organs.Any(o => o.Category == "Heart"), Is.False, "And no heart, not even a missing one.");
            });

            var dead = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(Debug.TryRemoveOrgan(dead, Part(dead, "Heart")));
            var deadScan = Analyzer.BuildScan(dead, true);
            Assert.Multiple(() =>
            {
                Assert.That(deadScan.CirculationStopped);
                Assert.That(deadScan.Unrevivable?.Id, Is.EqualTo("defibrillator-no-heart"), "The real reason, not a fixed sentence.");
            });

            Assert.That(Analyzer.BuildScan(SEntMan.SpawnAtPosition("MobMonkey", map.GridCoords), true).Target, Is.Not.Null);

            var lobotomized = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(Analyzer.BuildScan(lobotomized, true).Lobotomized, Is.False);
            SEntMan.AddComponent<LobotomizedComponent>(Part(lobotomized, "Head"));
            Assert.That(Analyzer.BuildScan(lobotomized, true).Lobotomized, "The lobotomy shows in the alerts.");

            Assert.That(Analyzer.BuildScan(lobotomized, true).XenoEmbryo, Is.False);
            SEntMan.AddComponent<XenoInfectedComponent>(lobotomized);
            Assert.That(Analyzer.BuildScan(lobotomized, true).XenoEmbryo, "The embryo shows in the alerts.");
        });
    }
}

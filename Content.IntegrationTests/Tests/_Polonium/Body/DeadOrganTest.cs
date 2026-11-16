using Content.Server._Polonium.Medical.HealthAnalyzer;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Server.Medical.HealthAnalyzer;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.Body;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// An organ is healthy down to 35% integrity, damaged below that and destroyed at zero.
/// A destroyed organ leaves a dead organ of the same category in the body, which does nothing
/// and can only be cut out and replaced.
/// </summary>
[TestFixture]
public sealed class DeadOrganTest : GameTest
{
    private const string Modifier = "DeadOrganTest";

    private EntityUid Organ(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    /// <summary>
    /// Destroys the organ of the given category and returns the dead organ left in its slot.
    /// </summary>
    private EntityUid Destroy(EntityUid body, string category)
    {
        var organ = Organ(body, category);
        var cap = SEntMan.GetComponent<OrganIntegrityComponent>(organ).IntegrityCap;
        SEntMan.System<TraumaSystem>().TrySetOrganDamageModifier(organ, cap, body, Modifier);
        return Organ(body, category);
    }

    [Test]
    public async Task ThresholdsFollowIntegrity()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var heart = Organ(body, "Heart");
            var integrity = SEntMan.GetComponent<OrganIntegrityComponent>(heart);
            var trauma = SEntMan.System<TraumaSystem>();
            var healthyFloor = integrity.IntegrityCap * 0.35f;

            trauma.TrySetOrganDamageModifier(heart, integrity.IntegrityCap - healthyFloor, body, Modifier);
            Assert.That(integrity.OrganSeverity, Is.EqualTo(OrganSeverity.Normal), "35% integrity is still healthy.");

            trauma.TrySetOrganDamageModifier(heart, integrity.IntegrityCap - healthyFloor + 0.01, body, Modifier);
            Assert.That(integrity.OrganSeverity, Is.EqualTo(OrganSeverity.Damaged), "Below 35% integrity the organ is damaged.");

            trauma.TrySetOrganDamageModifier(heart, integrity.IntegrityCap - 0.01, body, Modifier);
            Assert.That(integrity.OrganSeverity, Is.EqualTo(OrganSeverity.Damaged), "The organ only dies at zero integrity.");
            Assert.That(Organ(body, "Heart"), Is.EqualTo(heart));

            var brain = Organ(body, "Brain");
            var brainIntegrity = SEntMan.GetComponent<OrganIntegrityComponent>(brain);
            trauma.TrySetOrganDamageModifier(brain, brainIntegrity.IntegrityCap, body, Modifier);
            Assert.Multiple(() =>
            {
                Assert.That(brainIntegrity.IntegrityFloor, Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(brainIntegrity.OrganIntegrity, Is.EqualTo(brainIntegrity.IntegrityFloor), "The brain stops at its floor.");
                Assert.That(Organ(body, "Brain"), Is.EqualTo(brain));
            });
        });
    }

    [Test]
    public async Task DestroyedOrganLeavesDeadOrganWithSameParent()
    {
        var map = await Pair.CreateTestMap();
        string[] categories = ["Heart", "LungLeft", "Stomach"];
        var body = EntityUid.Invalid;
        var originals = new Dictionary<string, EntityUid>();
        var parents = new Dictionary<string, EntityUid?>();

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            foreach (var category in categories)
            {
                originals[category] = Organ(body, category);
                parents[category] = SEntMan.GetComponent<ChildOrganComponent>(originals[category]).Parent;
                Assert.That(parents[category], Is.Not.Null);
                Destroy(body, category);
            }
        });

        // Long enough for the deferred deletion of the old organs and for metabolism, breathing and digestion to tick.
        await Pair.RunTicksSync(300);

        await Server.WaitAssertion(() =>
        {
            foreach (var category in categories)
            {
                var dead = Organ(body, category);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.Deleted(originals[category]), $"{category}: the destroyed organ is gone.");
                    Assert.That(SEntMan.HasComponent<DeadOrganComponent>(dead), $"{category}: a dead organ took the slot.");
                    Assert.That(SEntMan.GetComponent<OrganComponent>(dead).Body, Is.EqualTo(body));
                    Assert.That(SEntMan.GetComponent<ChildOrganComponent>(dead).Parent, Is.EqualTo(parents[category]));
                    Assert.That(SEntMan.GetComponent<ParentOrganComponent>(parents[category]!.Value).Children, Does.Contain(dead));
                });
            }
        });
    }

    [Test]
    public async Task DeadOrganCanBeRemovedAndReplaced()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var torso = Organ(body, "Torso");
            var surgery = SEntMan.System<SurgerySystem>();

            bool Valid(string id)
            {
                var ev = new SurgeryValidEvent(body, torso, Category: "Torso");
                SEntMan.EventBus.RaiseLocalEvent(surgery.GetSingleton(id)!.Value, ref ev);
                return !ev.Cancelled;
            }

            var dead = Destroy(body, "Heart");
            Assert.Multiple(() =>
            {
                Assert.That(Valid("SurgeryRemoveHeart"), "A dead heart has to be removable.");
                Assert.That(Valid("SurgeryInsertHeart"), Is.False, "The dead heart still takes the slot.");
            });

            SEntMan.System<SharedContainerSystem>().Remove(dead, SEntMan.GetComponent<BodyComponent>(body).Organs!);
            Assert.Multiple(() =>
            {
                Assert.That(Valid("SurgeryInsertHeart"), "The slot is free once the dead heart is out.");
                Assert.That(Valid("SurgeryRemoveHeart"), Is.False, "Nothing is left to remove.");
            });
        });
    }

    [Test]
    public async Task RejuvenateReplacesDeadOrgan()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var dead = Destroy(body, "Heart");
            var liver = Organ(body, "Liver");
            var liverIntegrity = SEntMan.GetComponent<OrganIntegrityComponent>(liver);
            SEntMan.System<TraumaSystem>().TrySetOrganDamageModifier(liver, 10, body, Modifier);

            SEntMan.System<RejuvenateSystem>().PerformRejuvenate(body);
            Assert.That(liverIntegrity.OrganIntegrity, Is.EqualTo(liverIntegrity.IntegrityCap), "Rejuvenation heals damaged organs.");

            var heart = Organ(body, "Heart");
            Assert.Multiple(() =>
            {
                Assert.That(heart, Is.Not.EqualTo(dead));
                Assert.That(SEntMan.HasComponent<DeadOrganComponent>(heart), Is.False);
                Assert.That(SEntMan.HasComponent<OrganIntegrityComponent>(heart));
            });
        });
    }

    [Test]
    public async Task DeadEyesDoNotRestoreSight()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var blindable = SEntMan.GetComponent<BlindableComponent>(body);
            var dead = Destroy(body, "Eyes");
            Assert.That(blindable.MinDamage, Is.EqualTo(blindable.MaxDamage), "Destroyed eyes blind their owner.");

            // Transplanting dead eyes goes through the same container insert.
            var containers = SEntMan.System<SharedContainerSystem>();
            var organs = SEntMan.GetComponent<BodyComponent>(body).Organs!;
            containers.Remove(dead, organs);
            containers.Insert(dead, organs);
            Assert.That(blindable.MinDamage, Is.EqualTo(blindable.MaxDamage), "Dead eyes do not see.");
        });
    }

    [Test]
    public async Task AnalyzerReportsDeadOrganAsDestroyedNotMissing()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Destroy(body, "Heart");
            var heart = SEntMan.System<PatientScanSystem>().BuildScan(body, true).Organs
                .Where(organ => organ.Category == "Heart").ToList();
            Assert.Multiple(() =>
            {
                Assert.That(heart, Has.Count.EqualTo(1), "A dead heart is reported once, as dead, not as missing.");
                Assert.That(heart[0].Dead);
                Assert.That(heart[0].Missing, Is.False);
            });
        });
    }
}

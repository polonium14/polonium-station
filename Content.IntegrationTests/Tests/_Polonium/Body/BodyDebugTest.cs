using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Server.EUI;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// The body debug window's server actions: setting organ integrity through real traumas,
/// killing, reviving, removing and inserting organs.
/// </summary>
[TestFixture]
public sealed class BodyDebugTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private BodyDebugSystem Debug => SEntMan.System<BodyDebugSystem>();

    private EntityUid Organ(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    private OrganIntegrityComponent Integrity(EntityUid organ)
    {
        return SEntMan.GetComponent<OrganIntegrityComponent>(organ);
    }

    [Test]
    public async Task SliderDamagesThroughRealTraumaAndHealsBack()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var heart = Organ(body, "Heart");
            var torso = Organ(body, "Torso");
            var integrity = Integrity(heart);
            var traumas = SEntMan.System<TraumaSystem>();

            Assert.That(Debug.TrySetOrganIntegrity(body, heart, integrity.IntegrityCap / 2));
            Assert.Multiple(() =>
            {
                Assert.That(integrity.OrganIntegrity, Is.EqualTo(integrity.IntegrityCap / 2));
                Assert.That(traumas.HasWoundableTrauma(torso, TraumaType.OrganDamage), "The damage hangs on a real trauma.");
            });

            // Going further down grows the same trauma instead of wounding the torso again.
            var wounds = SEntMan.System<WoundSystem>().GetWoundableWounds(torso).Count();
            Assert.That(Debug.TrySetOrganIntegrity(body, heart, integrity.IntegrityCap * 0.4f));
            Assert.That(Debug.TrySetOrganIntegrity(body, heart, integrity.IntegrityCap * 0.36f));
            Assert.That(SEntMan.System<WoundSystem>().GetWoundableWounds(torso).Count(), Is.EqualTo(wounds));

            Assert.That(Debug.TrySetOrganIntegrity(body, heart, integrity.IntegrityCap * 0.35f));
            Assert.That(integrity.OrganSeverity, Is.EqualTo(OrganSeverity.Normal), "35% is exactly on the healthy threshold.");
            Assert.That(Debug.TrySetOrganIntegrity(body, heart, integrity.IntegrityCap * 0.34f));
            Assert.That(integrity.OrganSeverity, Is.EqualTo(OrganSeverity.Damaged));

            Assert.That(Debug.TrySetOrganIntegrity(body, heart, integrity.IntegrityCap));
            Assert.Multiple(() =>
            {
                Assert.That(integrity.OrganIntegrity, Is.EqualTo(integrity.IntegrityCap));
                Assert.That(traumas.HasWoundableTrauma(torso, TraumaType.OrganDamage), Is.False);
            });
        });
    }

    [Test]
    public async Task BrainStopsAtFloorAndComesBack()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var brain = Organ(body, "Brain");
            var integrity = Integrity(brain);

            Assert.That(Debug.TrySetOrganIntegrity(body, brain, FixedPoint2.Zero));
            Assert.That(integrity.OrganIntegrity, Is.EqualTo(integrity.IntegrityFloor));

            Assert.That(Debug.TrySetOrganIntegrity(body, brain, integrity.IntegrityCap / 2));
            Assert.That(integrity.OrganIntegrity, Is.EqualTo(integrity.IntegrityCap / 2));

            Assert.Multiple(() =>
            {
                Assert.That(Debug.TryKillOrgan(body, brain), Is.False);
                Assert.That(Debug.TryRemoveOrgan(body, brain), Is.False);
                Assert.That(Organ(body, "Brain"), Is.EqualTo(brain));
            });
        });
    }

    [Test]
    public async Task KillReviveRemoveInsert()
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        var removed = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var blindable = SEntMan.GetComponent<BlindableComponent>(body);
            var eyes = Organ(body, "Eyes");
            var parent = SEntMan.GetComponent<ChildOrganComponent>(eyes).Parent;

            Assert.That(Debug.TryKillOrgan(body, eyes));
            var dead = Organ(body, "Eyes");
            Assert.That(SEntMan.HasComponent<DeadOrganComponent>(dead));
            Assert.That(Debug.TryReviveOrgan(body, eyes), Is.False, "Only an organ inside the body can be revived.");

            Assert.That(Debug.TryReviveOrgan(body, dead));
            var fresh = Organ(body, "Eyes");
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<DeadOrganComponent>(fresh), Is.False);
                Assert.That(SEntMan.GetComponent<ChildOrganComponent>(fresh).Parent, Is.EqualTo(parent));
                Assert.That(blindable.MinDamage, Is.Zero, "Fresh eyes see again.");
            });

            removed = Organ(body, "Heart");
            var heartParent = SEntMan.GetComponent<ChildOrganComponent>(removed).Parent;
            Assert.That(Debug.TryRemoveOrgan(body, removed));
            Assert.That(Debug.GetOrgans(body).Single(row => row.Category == "Heart").Kind, Is.EqualTo(BodyDebugOrganKind.Missing));
            Assert.That(Debug.TryRemoveOrgan(body, Organ(body, "Torso")), Is.False, "Body parts are not organs to pull out.");

            Assert.That(Debug.TryInsertOrgan(body, "Heart"));
            Assert.That(Debug.TryInsertOrgan(body, "Heart"), Is.False, "The slot is taken.");
            var heart = Organ(body, "Heart");
            Assert.Multiple(() =>
            {
                Assert.That(heart, Is.Not.EqualTo(removed));
                Assert.That(SEntMan.GetComponent<ChildOrganComponent>(heart).Parent, Is.EqualTo(heartParent));
                Assert.That(Debug.GetOrgans(body).Single(row => row.Category == "Heart").Kind, Is.EqualTo(BodyDebugOrganKind.Alive));
            });
        });

        await Pair.RunTicksSync(60);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(removed), Is.False, "A removed organ stays in the world as an item.");
            Assert.That(SEntMan.GetComponent<OrganComponent>(removed).Body, Is.Null);
        });
    }

    /// <summary>
    /// Opens the window for the connected client with a damaged, a dead and a missing organ in the body.
    /// </summary>
    [Test]
    public async Task WindowOpensForClient()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(Debug.TrySetOrganIntegrity(body, Organ(body, "Liver"), Integrity(Organ(body, "Liver")).IntegrityCap * 0.2f));
            Assert.That(Debug.TryKillOrgan(body, Organ(body, "LungLeft")));
            Assert.That(Debug.TryRemoveOrgan(body, Organ(body, "Stomach")));

            var kinds = Debug.GetOrgans(body).Select(row => row.Kind).ToHashSet();
            Assert.That(kinds, Is.EquivalentTo(new[] { BodyDebugOrganKind.Alive, BodyDebugOrganKind.Dead, BodyDebugOrganKind.Missing }));

            var eui = new BodyDebugEui(Server.ResolveDependency<IDependencyCollection>(), body);
            Server.ResolveDependency<EuiManager>().OpenEui(eui, ServerSession!);
        });

        await RunUntilSynced();
    }
}

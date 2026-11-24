using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests._Polonium.Medical;
using Content.Server._Polonium.Body;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// A body without a working spleen stops regenerating blood and slowly loses it. Bodies that never had one are spared.
/// </summary>
[TestFixture]
public sealed class SpleenTest : GameTest
{
    private BodyDebugSystem Debug => SEntMan.System<BodyDebugSystem>();

    private EntityUid Organ(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task NoWorkingSpleenDrainsBlood(bool remove)
    {
        var map = await Pair.CreateTestMap();
        var patient = EntityUid.Invalid;
        var control = EntityUid.Invalid;
        var diona = EntityUid.Invalid;
        var bloodstream = SEntMan.System<BloodstreamSystem>();

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            control = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            diona = SEntMan.SpawnAtPosition("MobDiona", map.GridCoords);

            var spleen = Organ(patient, "Spleen");
            Assert.That(remove ? Debug.TryRemoveOrgan(patient, spleen) : Debug.TryKillOrgan(patient, spleen));
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<BloodDrainComponent>(patient));
                Assert.That(SEntMan.HasComponent<BloodDrainComponent>(control), Is.False, "A healthy spleen keeps the blood.");
                Assert.That(SEntMan.HasComponent<BloodDrainComponent>(diona), Is.False, "A body that never had a spleen is spared.");
            });
        });

        await Pair.RunSeconds(10);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(bloodstream.GetBloodLevel(patient), Is.LessThan(bloodstream.GetBloodLevel(control)),
                    "Without a working spleen the body loses blood.");
                Assert.That(bloodstream.GetBloodLevel(control), Is.EqualTo(1f).Within(0.001f));
            });

            // A new spleen stops the loss.
            if (!remove)
                Assert.That(Debug.TryRemoveOrgan(patient, Organ(patient, "Spleen")));

            Assert.That(Debug.TryInsertOrgan(patient, "Spleen"));
            Assert.That(SEntMan.HasComponent<BloodDrainComponent>(patient), Is.False);
        });
    }
}

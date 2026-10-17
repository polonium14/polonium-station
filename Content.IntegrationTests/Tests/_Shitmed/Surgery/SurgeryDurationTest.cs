using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.Body;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class SurgeryDurationTest : GameTest
{
    [TestCase(1f, 1f, false)]
    [TestCase(2f, 1f, false)]
    [TestCase(0.5f, 1f, false)]
    [TestCase(2f, 2f, true)]
    public async Task ScheduledStepAppliesEachSpeedModifierOnce(float surgeonSpeed, float toolSpeed, bool useTable)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity(null, coords);
            SEntMan.AddComponent<HandsComponent>(user);
            SEntMan.System<SharedHandsSystem>().AddHand(user, "right", HandLocation.Right);
            var doAfters = SEntMan.AddComponent<DoAfterComponent>(user);
            SEntMan.AddComponent<ScalpelComponent>(user).Speed = toolSpeed;
            SEntMan.AddComponent<SurgerySpeedModifierComponent>(user).SpeedModifier = surgeonSpeed;
            var body = SEntMan.SpawnEntity("HealedTraumaBody", coords);
            var torso = SEntMan.SpawnEntity("HealedTraumaTorso", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(torso, containers.GetContainer(body, BodyComponent.ContainerID));
            var tableSpeed = 1f;
            if (useTable)
            {
                SEntMan.AddComponent<BuckleComponent>(body);
                var table = SEntMan.SpawnEntity("OperatingTable", coords);
                Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(body, null, table, popup: false), Is.True);
                tableSpeed = SEntMan.GetComponent<OperatingTableComponent>(table).SpeedModifier;
            }

            var surgery = SEntMan.System<SurgerySystem>();
            Assert.That(surgery.TryDoSurgeryStep(body, torso, user, "SurgeryOpenIncision", "SurgeryStepOpenIncisionScalpel", out var error), Is.True, error.ToString());
            // Check the scheduled action, not only the duration helper: the old caller divided twice.
            var scheduled = doAfters.DoAfters.Values.Single();
            Assert.That(scheduled.Args.Delay.TotalSeconds,
                Is.EqualTo(2d / (surgeonSpeed * toolSpeed * tableSpeed)).Within(0.000001));
            SEntMan.System<SharedDoAfterSystem>().Cancel(user, scheduled.Index);
        });
    }
}

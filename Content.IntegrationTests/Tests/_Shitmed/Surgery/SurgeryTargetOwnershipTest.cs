using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class SurgeryTargetOwnershipTest : GameTest
{
    [Test]
    public async Task SurgeryMustStopWhenPartIsRemovedDuringOperation()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        EntityUid part = default;
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
            SEntMan.GetComponent<SurgeryTargetComponent>(body).SepsisImmune = true;
            part = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var user = SEntMan.SpawnEntity(null, coords);
            SEntMan.AddComponent<HandsComponent>(user);
            SEntMan.AddComponent<DoAfterComponent>(user);
            SEntMan.AddComponent<ScalpelComponent>(user);
            SEntMan.System<SharedHandsSystem>().AddHand(user, "right", HandLocation.Right);
            var containers = SEntMan.System<SharedContainerSystem>();
            var organs = containers.GetContainer(body, BodyComponent.ContainerID);
            containers.Insert(part, organs);
            Assert.That(SEntMan.System<SurgerySystem>().TryDoSurgeryStep(body, part, user,
                "SurgeryOpenIncision", "SurgeryStepOpenIncisionScalpel", out var error), Is.True, error.ToString());
            containers.Remove(part, organs);
        });
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(part), Is.False,
            "The old patient's pending surgery must not operate on a detached part."));
    }

}

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class OrganSurgeryReplicationTest : GameTest
{
    [Test]
    public async Task ClientTracksTreatableTraumaAndLeavesCompletionToServer()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        EntityUid body = default, part = default, heart = default, trauma = default, user = default;
        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnEntity("HealedTraumaBody", coords);
            part = SEntMan.SpawnEntity("HealedTraumaTorso", coords);
            SEntMan.AddComponent<DamageableComponent>(part);
            heart = SEntMan.SpawnEntity("HealedTraumaHeart", coords);
            var wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            var organs = containers.GetContainer(body, BodyComponent.ContainerID);
            containers.Insert(part, organs);
            containers.Insert(heart, organs);
            containers.Insert(wound, containers.GetContainer(part, WoundableComponent.WoundContainerId));
            SEntMan.System<WoundSystem>().SetWoundSeverity(wound, FixedPoint2.New(20));
            var traumas = SEntMan.System<TraumaSystem>();
            trauma = traumas.AddTrauma(heart, (part, SEntMan.GetComponent<WoundableComponent>(part)),
                (wound, SEntMan.GetComponent<TraumaInflicterComponent>(wound)), TraumaType.OrganDamage, FixedPoint2.New(35));
            traumas.TryCreateOrganDamageModifier(heart, FixedPoint2.New(35), trauma, "WoundableDamage");
            // Keep the organ injured even after the surgical modifier reaches zero.
            traumas.TryCreateOrganDamageModifier(heart, FixedPoint2.New(10), body, "OtherDamage");
            SEntMan.AddComponent<IncisionOpenComponent>(part);
            SEntMan.AddComponent<SkinRetractedComponent>(part);
            user = SEntMan.SpawnEntity(null, coords);
            SEntMan.AddComponent<HandsComponent>(user);
            SEntMan.AddComponent<HemostatComponent>(user);
            SEntMan.System<SharedHandsSystem>().AddHand(user, "right", HandLocation.Right);
        });
        await Pair.RunTicksSync(5);

        async Task CheckClient(bool treatable)
        {
            await Client.WaitAssertion(() =>
            {
                var clientBody = CEntMan.GetEntity(SEntMan.GetNetEntity(body));
                var clientPart = CEntMan.GetEntity(SEntMan.GetNetEntity(part));
                var surgery = CEntMan.System<Content.Client._Shitmed.Medical.Surgery.SurgerySystem>();
                var procedure = surgery.GetSingleton("SurgeryHealOrgans")!.Value;
                var valid = new SurgeryValidEvent(clientBody, clientPart, Category: "Torso");
                CEntMan.EventBus.RaiseLocalEvent(procedure, ref valid);
                Assert.That(valid.Cancelled, Is.EqualTo(!treatable), "Client and server must agree on organ surgery availability.");
                Assert.That(surgery.IsStepComplete(clientBody, clientPart, "SurgeryStepHealOrgans", procedure), Is.EqualTo(!treatable));
            });
        }

        try
        {
            await CheckClient(true);
            await Client.WaitAssertion(() =>
            {
                var clientBody = CEntMan.GetEntity(SEntMan.GetNetEntity(body));
                var clientPart = CEntMan.GetEntity(SEntMan.GetNetEntity(part));
                var clientUser = CEntMan.GetEntity(SEntMan.GetNetEntity(user));
                CEntMan.EnsureComponent<TestListenerComponent>(clientUser);
                var ev = new SurgeryDoAfterEvent("SurgeryHealOrgans", "SurgeryStepClampInternalBleeders", false);
                ev.DoAfter = new Content.Shared.DoAfter.DoAfter(0, new DoAfterArgs(CEntMan, clientUser, TimeSpan.Zero, ev, clientBody, clientPart), TimeSpan.Zero);
                CEntMan.EventBus.RaiseLocalEvent(clientBody, ev);
                Assert.That(CEntMan.HasComponent<InternalBleedersClampedComponent>(clientPart), Is.False,
                    "Predicted completion must not apply surgery effects locally.");
                Assert.That(CEntMan.System<SurgeryTreatmentCompletionTest.FailureListenerSystem>().Count(clientUser), Is.Zero,
                    "Only the server may declare a surgery failed.");
            });
            await Server.WaitAssertion(() => SEntMan.System<TraumaSystem>().TryChangeOrganDamageModifier(
                heart, FixedPoint2.New(-35), trauma, "WoundableDamage"));
            await Pair.RunTicksSync(5);
            await CheckClient(false);
            await Server.WaitAssertion(() => SEntMan.System<TraumaSystem>().TryChangeOrganDamageModifier(
                heart, FixedPoint2.New(35), trauma, "WoundableDamage"));
            await Pair.RunTicksSync(5);
            await CheckClient(true);
        }
        catch (Exception e)
        {
            TestContext.Out.WriteLine(e);
            throw;
        }
    }
}

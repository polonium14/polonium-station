using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Tourniquet;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Tourniquet;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Medical;
using Content.Shared.Medical.Healing;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
public sealed class OrganTransplantAnatomyTest : GameTest
{
    [Test]
    public async Task TransplantedOrganBelongsToRecipientsAnatomicalTree()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        try
        {
            await Server.WaitAssertion(() =>
            {
                var donor = SEntMan.SpawnEntity("MobHuman", coords);
                var recipient = SEntMan.SpawnEntity("MobHuman", coords);
                SEntMan.GetComponent<SurgeryTargetComponent>(donor).SepsisImmune = true;
                SEntMan.GetComponent<SurgeryTargetComponent>(recipient).SepsisImmune = true;
                var donorBody = SEntMan.GetComponent<BodyComponent>(donor);
                var recipientBody = SEntMan.GetComponent<BodyComponent>(recipient);
                Assert.That(LimbTargetMap.TryGetOrganByCategory(SEntMan, donorBody, "Torso", out var donorTorso), Is.True);
                Assert.That(LimbTargetMap.TryGetOrganByCategory(SEntMan, donorBody, "Liver", out var liver), Is.True);
                Assert.That(LimbTargetMap.TryGetOrganByCategory(SEntMan, recipientBody, "Torso", out var recipientTorso), Is.True);
                var surgery = SEntMan.System<SurgerySystem>();
                var remove = surgery.GetSingleton("SurgeryStepRemoveOrgan")!.Value;
                var removeProcedure = surgery.GetSingleton("SurgeryRemoveLiver")!.Value;
                var tweezers = SEntMan.SpawnEntity("Hemostat", coords);
                var ev = new SurgeryStepEvent(donor, donor, donorTorso, tweezers, removeProcedure, remove);
                SEntMan.EventBus.RaiseLocalEvent(remove, ref ev);
                ev = new SurgeryStepEvent(recipient, recipient, recipientTorso, tweezers, removeProcedure, remove);
                SEntMan.EventBus.RaiseLocalEvent(remove, ref ev);
                var insert = surgery.GetSingleton("SurgeryStepInsertLiver")!.Value;
                ev = new SurgeryStepEvent(recipient, recipient, recipientTorso, liver, surgery.GetSingleton("SurgeryInsertLiver")!.Value, insert);
                SEntMan.EventBus.RaiseLocalEvent(insert, ref ev);
                Assert.That(SEntMan.GetComponent<OrganComponent>(liver).Body, Is.EqualTo(recipient));
                Assert.That(SEntMan.GetComponent<ChildOrganComponent>(liver).Parent, Is.EqualTo(recipientTorso),
                    "The donor's torso must no longer own a transplanted liver.");
                Assert.That(SEntMan.GetComponent<ParentOrganComponent>(donorTorso).Children, Does.Not.Contain(liver));
            });
        }
        catch (Exception e)
        {
            TestContext.Out.WriteLine(e);
            throw;
        }
    }
}

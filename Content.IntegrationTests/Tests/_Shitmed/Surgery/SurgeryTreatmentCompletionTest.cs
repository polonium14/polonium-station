using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
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
public sealed class SurgeryTreatmentCompletionTest : GameTest
{
    public sealed class FailureListenerSystem : TestListenerSystem<SurgeryStepFailedEvent>;
    public sealed class CompletionListenerSystem : TestListenerSystem<SurgeryStepEvent>;

    // Reuse the minimal body, torso, heart and wound prototypes from HealedWoundTraumaTreatmentTest.
    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 1)]
    [TestCase(true, 1)]
    [TestCase(false, 2)]
    [TestCase(true, 2)]
    [TestCase(true, 3)]
    public async Task TreatmentOnlyReportsActualInterruptions(bool organTreatment, int completionMode)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        var surgery = SEntMan.System<SurgerySystem>();
        var traumas = SEntMan.System<TraumaSystem>();
        var wounds = SEntMan.System<WoundSystem>();
        var failures = SEntMan.System<FailureListenerSystem>();
        var completions = SEntMan.System<CompletionListenerSystem>();
        var type = organTreatment ? TraumaType.OrganDamage : TraumaType.BoneDamage;
        var surgeryId = organTreatment ? "SurgeryHealOrgans" : "SurgeryMendBones";
        var stepId = organTreatment ? "SurgeryStepHealOrgans" : "SurgeryStepMendBones";
        EntityUid user = default, body = default, torso = default, heart = default, bone = default;

        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity(null, coords);
            SEntMan.AddComponent<HandsComponent>(user);
            SEntMan.AddComponent<DoAfterComponent>(user);
            SEntMan.AddComponent<TestListenerComponent>(user);
            // An empty-handed tool user keeps the test focused on the treatment DoAfter.
            SEntMan.AddComponent<BoneGelComponent>(user);
            SEntMan.AddComponent<TendingComponent>(user);
            SEntMan.System<SharedHandsSystem>().AddHand(user, "right", HandLocation.Right);

            body = SEntMan.SpawnEntity("HealedTraumaBody", coords);
            torso = SEntMan.SpawnEntity("HealedTraumaTorso", coords);
            SEntMan.AddComponent<DamageableComponent>(torso);
            heart = SEntMan.SpawnEntity("HealedTraumaHeart", coords);
            var wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            var organs = containers.GetContainer(body, BodyComponent.ContainerID);
            containers.Insert(torso, organs);
            containers.Insert(heart, organs);
            containers.Insert(wound, containers.GetContainer(torso, WoundableComponent.WoundContainerId));
            wounds.SetWoundSeverity(wound, FixedPoint2.New(20));

            SEntMan.AddComponent<IncisionOpenComponent>(torso);
            SEntMan.AddComponent<SkinRetractedComponent>(torso);
            SEntMan.AddComponent<BonesSawedComponent>(torso);
            SEntMan.AddComponent<InternalBleedersClampedComponent>(torso);
            var woundable = SEntMan.GetComponent<WoundableComponent>(torso);
            var inflicter = SEntMan.GetComponent<TraumaInflicterComponent>(wound);
            bone = woundable.Bone!.ContainedEntities.Single();
            if (organTreatment)
            {
                var trauma = traumas.AddTrauma(heart, (torso, woundable), (wound, inflicter), type, FixedPoint2.New(35));
                traumas.TryCreateOrganDamageModifier(heart, FixedPoint2.New(35), trauma, "WoundableDamage");
            }
            else
                traumas.ApplyBoneTrauma(bone, (torso, woundable), (wound, inflicter), FixedPoint2.New(35));

            Assert.That(surgery.TryDoSurgeryStep(body, torso, user, surgeryId, stepId, out var error), Is.True, error.ToString());

            if (completionMode == 1)
            {
                // Another treatment finishes the injury while this surgeon is still working.
                if (organTreatment)
                {
                    var integrity = SEntMan.GetComponent<OrganIntegrityComponent>(heart);
                    foreach (var key in integrity.IntegrityModifiers.Keys.ToArray())
                        traumas.TryRemoveOrganDamageModifier(heart, key.Item2, key.Item1);
                }
                else
                    traumas.SetBoneIntegrity(bone, SEntMan.GetComponent<BoneComponent>(bone).IntegrityCap);
            }
            else if (completionMode == 2)
            {
                var doAfter = SEntMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single();
                SEntMan.System<SharedDoAfterSystem>().Cancel(user, doAfter.Index);
            }
            else if (completionMode == 3)
            {
                // The target organ leaves the patient while treatment is in progress.
                containers.Remove(heart, organs);
            }
        });

        // Production treatment takes seven seconds per pass, and 35 damage needs three passes.
        await Pair.RunSeconds(25);

        await Server.WaitAssertion(() =>
        {
            Assert.That(failures.Count(user), Is.EqualTo(completionMode == 2 ? 1 : 0));
            Assert.That(completions.Count(user), Is.EqualTo(completionMode == 0 ? 3 : 0));
            Assert.That(traumas.HasWoundableTrauma(torso, type), Is.EqualTo(completionMode is 2 or 3));
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(user).DoAfters.Values
                .Any(d => !d.Completed && !d.Cancelled), Is.False);
        });
    }
}

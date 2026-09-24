using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class HealedWoundTraumaTreatmentTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HealedTraumaBody
  components:
  - type: Body
  - type: SurgeryTarget

- type: entity
  id: HealedTraumaTorso
  components:
  - type: Organ
    category: Torso
  - type: Woundable
    integrityCap: 100
    healAbility: 0
    thresholds:
      Healthy: 100
      Minor: 80
      Moderate: 60
      Severe: 40
      Critical: 20
      Mangled: 7
      Severed: 0

- type: entity
  id: HealedTraumaHeart
  components:
  - type: Organ
    category: Heart
  - type: OrganIntegrity
    integrityCap: 100
    integrityThresholds:
      Normal: 100
      Damaged: 50
      Destroyed: 0

- type: entity
  id: HealedTraumaWound
  components:
  - type: Wound
    damageType: Blunt
  - type: TraumaInflicter
    allowedTraumas: []
";

    [TestCase(TraumaType.BoneDamage, "SurgeryMendBones", "SurgeryStepMendBones")]
    [TestCase(TraumaType.OrganDamage, "SurgeryHealOrgans", "SurgeryStepHealOrgans")]
    public async Task FullyHealedWoundKeepsTraumaTreatable(TraumaType type, string surgeryId, string stepId)
    {
        var wounds = SEntMan.System<WoundSystem>();
        var traumas = SEntMan.System<TraumaSystem>();
        var surgery = SEntMan.System<SurgerySystem>();
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        EntityUid body = default, torso = default, heart = default, bone = default, wound = default;

        await Server.WaitPost(() =>
        {
            body = SEntMan.SpawnEntity("HealedTraumaBody", coords);
            torso = SEntMan.SpawnEntity("HealedTraumaTorso", coords);
            heart = SEntMan.SpawnEntity("HealedTraumaHeart", coords);
            wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            var organs = containers.GetContainer(body, BodyComponent.ContainerID);
            containers.Insert(torso, organs);
            containers.Insert(heart, organs);
            containers.Insert(wound, containers.GetContainer(torso, WoundableComponent.WoundContainerId));
            wounds.SetWoundSeverity(wound, FixedPoint2.New(20));

            var woundable = SEntMan.GetComponent<WoundableComponent>(torso);
            var inflicter = SEntMan.GetComponent<TraumaInflicterComponent>(wound);
            bone = woundable.Bone!.ContainedEntities.Single();
            if (type == TraumaType.BoneDamage)
            {
                var boneComp = SEntMan.GetComponent<BoneComponent>(bone);
                traumas.ApplyBoneTrauma(bone, (torso, woundable), (wound, inflicter), boneComp.IntegrityCap);
            }
            else
            {
                var trauma = traumas.AddTrauma(heart, (torso, woundable), (wound, inflicter), type, FixedPoint2.New(20));
                traumas.TryCreateOrganDamageModifier(heart, FixedPoint2.New(20), trauma, "WoundableDamage");
            }

            Assert.That(wounds.TryHealWoundsOnWoundable(torso, FixedPoint2.New(100), out _), Is.True);
        });

        // Let queued deletions run: checking only the bone immediately after healing misses
        // the loss of the trauma entity and the surgery that depends on it.
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(wounds.HasDamageOfType(torso, "Blunt"), Is.False);
            Assert.That(wounds.GetWoundableSeverityPoint(torso), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(traumas.HasWoundableTrauma(torso, type), Is.True);
            if (type == TraumaType.BoneDamage)
                Assert.That(SEntMan.GetComponent<BoneComponent>(bone).BoneSeverity, Is.EqualTo(BoneSeverity.Broken));
            else
                Assert.That(SEntMan.GetComponent<OrganIntegrityComponent>(heart).OrganIntegrity, Is.EqualTo(FixedPoint2.New(80)));

            var surgeryEnt = surgery.GetSingleton(surgeryId)!.Value;
            var valid = new SurgeryValidEvent(body, torso, Category: "Torso");
            SEntMan.EventBus.RaiseLocalEvent(surgeryEnt, ref valid);
            Assert.That(valid.Cancelled, Is.False, "Healing flesh must not hide trauma treatment.");

            var step = surgery.GetSingleton(stepId)!.Value;
            for (var i = 0; i < 10 && traumas.HasWoundableTrauma(torso, type); i++)
            {
                var ev = new SurgeryStepEvent(body, body, torso, EntityUid.Invalid, surgeryEnt, step);
                SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            }

            Assert.That(traumas.HasWoundableTrauma(torso, type), Is.False);
            if (type == TraumaType.BoneDamage)
                Assert.That(SEntMan.GetComponent<BoneComponent>(bone).BoneSeverity, Is.EqualTo(BoneSeverity.Normal));
            else
                Assert.That(SEntMan.GetComponent<OrganIntegrityComponent>(heart).OrganIntegrity, Is.EqualTo(FixedPoint2.New(100)));
        });

        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(wound), Is.True, "The healed wound should be released after its last trauma is treated.");
            Assert.That(wounds.GetWoundableWounds(torso), Is.Empty);
        });
    }
}

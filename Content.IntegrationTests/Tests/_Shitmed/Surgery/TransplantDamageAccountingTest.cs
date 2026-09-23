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
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class TransplantDamageAccountingTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [Test]
    public async Task SealingDonorLimbMustNotEraseRecipientsOtherDamage()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var recipient = SEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
            SEntMan.GetComponent<SurgeryTargetComponent>(recipient).SepsisImmune = true;
            var torso = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var arm = SEntMan.SpawnEntity("TendWoundsStepTestArmOrgan", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(torso, containers.GetContainer(recipient, BodyComponent.ContainerID));
            var damage = SEntMan.System<DamageableSystem>();
            var wounds = SEntMan.System<WoundSystem>();
            var blunt = new DamageSpecifier(SProtoMan.Index(BluntDamageType), FixedPoint2.New(8));
            damage.TryChangeDamage(torso, blunt);
            damage.TryChangeDamage(arm, blunt);
            Assert.That(wounds.GetTypeDamage(recipient, "Blunt"), Is.EqualTo(FixedPoint2.New(8)));
            var surgery = SEntMan.System<SurgerySystem>();
            var procedure = surgery.GetSingleton("SurgeryAttachLeftArm")!.Value;
            var insert = surgery.GetSingleton("SurgeryStepInsertArmLeft")!.Value;
            var ev = new SurgeryStepEvent(recipient, recipient, torso, arm, procedure, insert);
            SEntMan.EventBus.RaiseLocalEvent(insert, ref ev);
            Assert.That(wounds.GetTypeDamage(recipient, "Blunt"), Is.EqualTo(FixedPoint2.New(16)));
            var seal = surgery.GetSingleton("SurgeryStepSealWounds")!.Value;
            var cautery = SEntMan.SpawnEntity("Cautery", coords);
            ev = new SurgeryStepEvent(recipient, recipient, torso, cautery, procedure, seal);
            SEntMan.EventBus.RaiseLocalEvent(seal, ref ev);
            Assert.That(wounds.GetTypeDamage(torso, "Blunt"), Is.EqualTo(FixedPoint2.New(8)));
            Assert.That(wounds.GetTypeDamage(arm, "Blunt"), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(wounds.GetTypeDamage(recipient, "Blunt"), Is.GreaterThanOrEqualTo(FixedPoint2.New(8)),
                "The recipient still has 8 torso damage; healing a newly inserted arm must not erase it from the body total.");
        });
    }
    [Test]
    public async Task RemovingAndReinsertingAnInjuredPartTransfersDamageOnce()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
            var torso = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var arm = SEntMan.SpawnEntity("TendWoundsStepTestArmOrgan", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            var organs = containers.GetContainer(body, BodyComponent.ContainerID);
            containers.Insert(torso, organs);
            containers.Insert(arm, organs);
            var damage = SEntMan.System<DamageableSystem>();
            var blunt = new DamageSpecifier(SProtoMan.Index(BluntDamageType), FixedPoint2.New(8));
            damage.TryChangeDamage(torso, blunt);
            damage.TryChangeDamage(arm, blunt);
            Assert.That(damage.GetTotalDamage(body), Is.EqualTo(FixedPoint2.New(16)));
            containers.Remove(arm, organs);
            Assert.That(damage.GetTotalDamage(body), Is.EqualTo(FixedPoint2.New(8)));
            Assert.That(damage.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.New(8)));
            containers.Insert(arm, organs);
            Assert.That(damage.GetTotalDamage(body), Is.EqualTo(FixedPoint2.New(16)));
            Assert.That(damage.GetTotalDamage(arm), Is.EqualTo(FixedPoint2.New(8)));
        });
    }
}

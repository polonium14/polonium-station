using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

/// <summary>
/// Live playtest report: "i cant fix burn damage with surgery, there is no such surgery."
/// TryCreateWound (WoundSystem.Queries.cs) refuses to spawn a wound at all when severity is
/// below WoundThresholds[Minor] scaled by the woundable's own IntegrityCap - a torso
/// (IntegrityCap 200) needs 2.0+ damage in one hit to ever get a Heat wound, while a groin
/// (IntegrityCap 100) only needs 1.0+. Diffuse/environmental Heat sources (e.g. standing in
/// fire) split evenly across limbs via ApplyToAllLimbs and can land well under a large organ's
/// threshold every tick - the damage still lands on the organ's own DamageableComponent, but
/// no wound is ever created, so SurgeryWoundedCondition (which only checks for a wound entity)
/// hid the Tend Burn Wounds surgery entirely, and nothing ever healed the stuck raw damage.
/// Fixed by having both OnWoundedValid (SharedSurgerySystem.cs) and OnTendWoundsStep
/// (SharedSurgerySystem.Steps.cs) fall back to WoundSystem.GetGroupDamage - the organ's raw
/// per-group damage total - whenever no wound of that damage group exists.
/// </summary>
[TestFixture]
public sealed class BurnSurgeryAvailableBelowWoundThresholdTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> HeatDamageType = "Heat";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: BurnThresholdTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: SurgeryTarget

- type: entity
  id: BurnThresholdTestTorsoOrgan
  components:
  - type: Organ
    category: Torso
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 200
    healAbility: 0
    thresholds:
      Healthy: 200
      Minor: 160
      Moderate: 120
      Severe: 80
      Critical: 40
      Mangled: 14
      Severed: 0
";

    [Test]
    public async Task SmallHeatDamageMakesNoWoundButSurgeryStillAvailableAndHeals()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sWounds = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("BurnThresholdTestVictim", coords);
            torso = sEntMan.SpawnEntity("BurnThresholdTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
        });

        await pair.RunTicksSync(5);

        // 1.5 Heat is below this torso's minorThreshold (WoundThresholds[Minor]=1 * 200/100=2) -
        // matches the real 1.5/1.35-per-tick values from the reported server log.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(HeatDamageType);
            sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New("1.5")));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var totalDamage = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618
            Assert.That(totalDamage, Is.EqualTo(FixedPoint2.New("1.5")),
                "Sanity check: the sub-threshold damage should still land on the organ.");

            var wounds = sWounds.GetWoundableWounds(torso).ToList();
            Assert.That(wounds, Is.Empty,
                "Sanity check: 1.5 damage on a 200-cap organ should be below TryCreateWound's minorThreshold and produce no wound at all.");
        });

        // Confirm the surgery is now available purely off raw damage, with no wound present.
        await server.WaitAssertion(() =>
        {
            var surgeryEnt = sSurgery.GetSingleton("SurgeryTendWoundsBurn");
            Assert.That(surgeryEnt, Is.Not.Null, "SurgeryTendWoundsBurn should resolve to a real singleton entity.");
            Assert.That(sEntMan.HasComponent<SurgeryWoundedConditionComponent>(surgeryEnt!.Value), Is.True);

            var ev = new SurgeryValidEvent(victim, torso);
            sEntMan.EventBus.RaiseLocalEvent(surgeryEnt!.Value, ref ev);

            Assert.That(ev.Cancelled, Is.False,
                "The wounded-condition check previously cancelled here because no wound entity existed - " +
                "the exact 'there is no such surgery' bug this test guards against.");
        });

        // Confirm the tend step actually heals the raw damage too, not just makes the surgery visible.
        var stepEnt = default(EntityUid?);
        var burnSurgeryEnt = default(EntityUid?);
        await server.WaitAssertion(() =>
        {
            stepEnt = sSurgery.GetSingleton("SurgeryStepRepairBurnTissue");
            burnSurgeryEnt = sSurgery.GetSingleton("SurgeryTendWoundsBurn");
            Assert.That(stepEnt, Is.Not.Null);
        });

        for (var i = 0; i < 500; i++)
        {
            await server.WaitPost(() =>
            {
                var ev = new SurgeryStepEvent(victim, victim, torso, EntityUid.Invalid, burnSurgeryEnt!.Value, stepEnt!.Value, false);
                sEntMan.EventBus.RaiseLocalEvent(stepEnt!.Value, ref ev);
            });

            await pair.RunTicksSync(1);

            var stillDamaged = false;
            await server.WaitAssertion(() =>
            {
#pragma warning disable CS0618
                stillDamaged = sDamageable.GetTotalDamage(torso) > FixedPoint2.Zero;
#pragma warning restore CS0618
            });

            if (!stillDamaged)
                break;
        }

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var finalDamage = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618
            Assert.That(finalDamage, Is.EqualTo(FixedPoint2.Zero),
                "The wound-less raw damage should be fully healable via repeated tend-wound clicks, not stuck forever.");
        });
    }
}

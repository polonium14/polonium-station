using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Healing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// Live playtest report: "still cant heal with ointment if torso already healed fully - if there
/// is still some burn damage ointment should heal." A first pass fixed HealingSystem.OnDoAfter's
/// per-type split loop (see this test's own prior revision), but the report persisted after a
/// clean rebuild - because the real blocker sits one step earlier, in TryHeal's pre-use gate
/// (HasDamage), which OnDoAfter's fix can never run if this gate already refused the item.
///
/// Root cause: HasDamage checked the MOB's own aggregate DamageableComponent, never the
/// specifically targeted organ. Untargeted damage (fire, no TargetingComponent origin) fans out
/// through BodyDamageBridgeSystem.ApplyToAllLimbs, which gives EVERY organ its own weighted share
/// of the SAME event under SkipOrganMobSyncComponent (no mirror back to the mob at fan-out time)
/// - so organs collectively end up holding far more raw damage than the mob's own direct total
/// ever recorded. Healing one organ's real wound DOES mirror its heal back onto the mob total
/// (OnOrganDamageChanged has no such skip), so a single big-weight organ (e.g. torso, weight 1.0)
/// being healed can drain the mob aggregate to zero while other organs (e.g. an arm, weight 0.3)
/// still carry real, untouched damage under their own minorThreshold (never having formed a
/// wound at all). HasDamage then reads that drained aggregate and refuses the item outright -
/// "medical-item-cant-use" - even with the OnDoAfter fix in place, because OnDoAfter never runs.
///
/// Fixed by making HasDamage resolve the same targeted organ OnDoAfter already does and check
/// its own raw damage (WoundSystem.GetTypeDamage) instead of the mob aggregate. Drives the real
/// AfterInteractEvent -> TryHeal -> HasDamage entry point (not a hand-built DoAfter event) so
/// this actually exercises the fixed gate, not just OnDoAfter in isolation - a raw-DoAfter-event
/// test would have kept passing throughout this whole investigation without ever catching the
/// live bug.
/// </summary>
[TestFixture]
[TestOf(typeof(HealingSystem))]
public sealed class HealingWoundlessOrganDamageTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> HeatDamageType = "Heat";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HealBurnGateTestPatient
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: Targeting
  - type: Hands
  - type: DoAfter

- type: entity
  id: HealBurnGateTestTorsoOrgan
  components:
  - type: Organ
    category: Torso
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 200
    thresholds:
      Healthy: 200
      Minor: 160
      Moderate: 120
      Severe: 80
      Critical: 40
      Mangled: 14
      Severed: 0

- type: entity
  id: HealBurnGateTestArmOrgan
  components:
  - type: Organ
    category: ArmLeft
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 80
    thresholds:
      Healthy: 80
      Minor: 64
      Moderate: 48
      Severe: 32
      Critical: 16
      Mangled: 6
      Severed: 0

- type: entity
  id: HealBurnGateTestItem
  components:
  - type: Healing
    delay: 0.1
    selfHealPenaltyMultiplier: 1
    damage:
      types:
        Heat: -50
";

    [Test]
    public async Task HealingTorsoOrganAfterUntargetedFireDamageDoesNotStrandArmsRawDamage()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid patient = default;
        EntityUid torso = default;
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            patient = sEntMan.SpawnEntity("HealBurnGateTestPatient", coords);
            torso = sEntMan.SpawnEntity("HealBurnGateTestTorsoOrgan", coords);
            arm = sEntMan.SpawnEntity("HealBurnGateTestArmOrgan", coords);

            sEntMan.System<SharedHandsSystem>().AddHand(patient, "right", HandLocation.Right);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(patient, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
            container.Insert(arm, organsContainer);
        });

        await pair.RunTicksSync(5);

        // Untargeted (origin: null) Heat damage straight to the mob, matching real fire/reagent
        // damage - fans out via ApplyToAllLimbs, which now normalizes the weight table (Torso 1.0,
        // Arm 0.3) to sum to the real damage. Of 3.0: the arm takes 3.0*0.3/1.3 = 0.69 (under its
        // own minorThreshold 1 * 80/100 = 0.8, so real damage but no wound), and the torso (anchor)
        // soaks the remainder 2.31 - over its own threshold (1 * 200/100 = 2), forming a real wound.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(HeatDamageType);
            sDamageable.TryChangeDamage(patient, new DamageSpecifier(proto, FixedPoint2.New("3.0")), ignoreResistances: true, origin: null);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.New("2.31")), "Sanity: torso got the normalized remainder and a real wound.");
            Assert.That(sDamageable.GetTotalDamage(arm), Is.EqualTo(FixedPoint2.New("0.69")), "Sanity: arm got its normalized, wound-less share.");
#pragma warning restore CS0618
        });

        // Heal the TORSO through the real interaction entry point.
        await server.WaitPost(() =>
        {
            var targeting = sEntMan.GetComponent<TargetingComponent>(patient);
            targeting.Target = TargetBodyPart.Chest;

            var item = sEntMan.SpawnEntity("HealBurnGateTestItem", coords);
            var patientCoords = sEntMan.GetComponent<TransformComponent>(patient).Coordinates;
            var ev = new AfterInteractEvent(patient, item, patient, patientCoords, true);
            sEntMan.EventBus.RaiseLocalEvent(item, ev);
        });

        await pair.RunSeconds(1f);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.Zero), "Torso's real wound should be fully healed.");
            Assert.That(sDamageable.GetTotalDamage(arm), Is.EqualTo(FixedPoint2.New("0.69")), "Arm's own raw damage must be untouched by a heal that targeted the torso.");
#pragma warning restore CS0618
        });

        // Retarget to the still-burnt arm and try to heal it - through the same real entry
        // point, so a refusal at HasDamage (never reaching OnDoAfter) is actually caught.
        await server.WaitPost(() =>
        {
            var targeting = sEntMan.GetComponent<TargetingComponent>(patient);
            targeting.Target = TargetBodyPart.LeftArm;

            var item = sEntMan.SpawnEntity("HealBurnGateTestItem", coords);
            var patientCoords = sEntMan.GetComponent<TransformComponent>(patient).Coordinates;
            var ev = new AfterInteractEvent(patient, item, patient, patientCoords, true);
            sEntMan.EventBus.RaiseLocalEvent(item, ev);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var armDamage = sDamageable.GetTotalDamage(arm);
#pragma warning restore CS0618
            Assert.That(armDamage, Is.EqualTo(FixedPoint2.Zero),
                "The arm's real, wound-less damage should actually heal when targeted directly through the real " +
                "interaction entry point. The old bug's HasDamage gate read the mob aggregate (drained to zero by " +
                "healing the torso above) and refused the item outright - 'medical-item-cant-use' - before " +
                "OnDoAfter ever ran, exactly matching 'still cant heal with ointment if torso already healed " +
                "fully' even with OnDoAfter's own fix already in place.");
        });
    }
}

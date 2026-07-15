using System.Numerics;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
[TestOf(typeof(Content.Server._Shitmed.Medical.Surgery.SurgerySystem))]
public sealed class SepsisTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> PoisonDamageType = "Poison";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SepsisTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: SurgeryTarget

- type: entity
  id: SepsisTestTorsoOrgan
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
  id: SepsisTestLegOrgan
  components:
  - type: Organ
    category: LegLeft
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
  id: SepsisTestAttackerWithTargeting
  components:
  - type: Targeting
";

    [Test]
    public async Task SepsisDamageHitsBothTheOrganAndTheMob()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity(null, coords);
            victim = sEntMan.SpawnEntity("SepsisTestVictim", coords);
            organ = sEntMan.SpawnEntity("SepsisTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        FixedPoint2 organDamageBefore = default;
        FixedPoint2 mobDamageBefore = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            organDamageBefore = sDamageable.GetTotalDamage(organ);
            mobDamageBefore = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618
        });

        // Mirrors HandleSanitization's own construction exactly: 5 Poison at a 0.5
        // partMultiplier, raised as SurgeryStepDamageEvent on the mob (SurgeryTargetComponent
        // lives on the body, matching where InitialBodySystem adds it for real players).
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PoisonDamageType);
            var sepsis = new DamageSpecifier(proto, FixedPoint2.New(5));
            var ev = new SurgeryStepDamageEvent(attacker, victim, organ, EntityUid.Invalid, sepsis, 0.5f);
            sEntMan.EventBus.RaiseLocalEvent(victim, ref ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var organDamageAfter = sDamageable.GetTotalDamage(organ);
            var mobDamageAfter = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618

            Assert.That(organDamageAfter, Is.GreaterThan(organDamageBefore),
                "Sepsis damage should register on the targeted organ's own DamageableComponent.");
            Assert.That(organDamageAfter - organDamageBefore, Is.EqualTo(FixedPoint2.New(2.5)),
                "5 Poison at a 0.5 partMultiplier should land as exactly 2.5 on the organ.");

            Assert.That(mobDamageAfter, Is.GreaterThan(mobDamageBefore),
                "Sepsis damage should also register on the mob's own DamageableComponent - that's what the health analyzer's total-damage readout and crit/death thresholds actually read.");
            Assert.That(mobDamageAfter - mobDamageBefore, Is.EqualTo(FixedPoint2.New(2.5)),
                "The mob should take the same 2.5 the organ took, not a multiplied or re-mirrored amount.");
        });
    }

    /// <summary>
    /// Proves the origin: null reasoning in SetDamage's own comment isn't just theoretical:
    /// the surgeon has their own TargetingComponent selection (defaults to Chest -&gt; Torso),
    /// unrelated to which limb is actually being operated on (here, the leg). If the mob-level
    /// TryChangeDamage call kept origin: user, BodyDamageBridgeSystem would re-mirror that same
    /// damage onto the Torso organ too, on top of the correctly-targeted leg - this asserts the
    /// Torso organ stays completely untouched.
    /// </summary>
    [Test]
    public async Task SepsisDamageDoesNotDoubleHitTheSurgeonsOwnTargetedOrgan()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid legOrgan = default;
        EntityUid torsoOrgan = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("SepsisTestAttackerWithTargeting", coords);
            victim = sEntMan.SpawnEntity("SepsisTestVictim", coords);
            legOrgan = sEntMan.SpawnEntity("SepsisTestLegOrgan", coords);
            torsoOrgan = sEntMan.SpawnEntity("SepsisTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(legOrgan, organsContainer);
            container.Insert(torsoOrgan, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var targeting = sEntMan.GetComponent<TargetingComponent>(attacker);
            Assert.That(targeting.Target, Is.EqualTo(TargetBodyPart.Chest),
                "Sanity check: attacker's own target should default to Chest (-> Torso), different from the leg being operated on.");
        });

        // Surgery is being performed on the leg, not the torso - deliberately mismatched
        // against the attacker's own (irrelevant) Chest target.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PoisonDamageType);
            var sepsis = new DamageSpecifier(proto, FixedPoint2.New(5));
            var ev = new SurgeryStepDamageEvent(attacker, victim, legOrgan, EntityUid.Invalid, sepsis, 0.5f);
            sEntMan.EventBus.RaiseLocalEvent(victim, ref ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var legDamage = sDamageable.GetTotalDamage(legOrgan);
            var torsoDamage = sDamageable.GetTotalDamage(torsoOrgan);
#pragma warning restore CS0618

            Assert.That(legDamage, Is.EqualTo(FixedPoint2.New(2.5)),
                "The actual surgery-targeted leg organ should take the sepsis damage.");
            Assert.That(torsoDamage, Is.EqualTo(FixedPoint2.Zero),
                "The surgeon's own unrelated Chest/Torso target should NOT take any damage from a leg-targeted surgery step.");
        });
    }

    [Test]
    public async Task HealingOneOrganDoesNotStealMobCreditFromAnotherOrgansUnrelatedDamage()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid legOrgan = default;
        EntityUid torsoOrgan = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity(null, coords);
            victim = sEntMan.SpawnEntity("SepsisTestVictim", coords);
            legOrgan = sEntMan.SpawnEntity("SepsisTestLegOrgan", coords);
            torsoOrgan = sEntMan.SpawnEntity("SepsisTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(legOrgan, organsContainer);
            container.Insert(torsoOrgan, organsContainer);
        });

        await pair.RunTicksSync(5);

        // Both the leg and the torso carry Poison damage - the leg's is deliberately untouched
        // by anything that follows, it should survive completely intact.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PoisonDamageType);
            sDamageable.TryChangeDamage(legOrgan, new DamageSpecifier(proto, FixedPoint2.New(10)), ignoreResistances: true);
            sDamageable.TryChangeDamage(torsoOrgan, new DamageSpecifier(proto, FixedPoint2.New(2)), ignoreResistances: true);
        });

        await pair.RunTicksSync(5);

        FixedPoint2 mobDamageBefore = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            mobDamageBefore = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618
            Assert.That(mobDamageBefore, Is.EqualTo(FixedPoint2.New(12)),
                "Sanity check: mob total should be the leg's 10 plus the torso's 2 before any healing.");
        });

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PoisonDamageType);
            var heal = new DamageSpecifier(proto, FixedPoint2.New(-6));
            var ev = new SurgeryStepDamageEvent(attacker, victim, torsoOrgan, EntityUid.Invalid, heal, 1f);
            sEntMan.EventBus.RaiseLocalEvent(victim, ref ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var legDamageAfter = sDamageable.GetTotalDamage(legOrgan);
            var torsoDamageAfter = sDamageable.GetTotalDamage(torsoOrgan);
            var mobDamageAfter = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618

            Assert.That(legDamageAfter, Is.EqualTo(FixedPoint2.New(10)),
                "The leg's own damage was never touched by this heal - it should be completely unchanged.");
            Assert.That(torsoDamageAfter, Is.EqualTo(FixedPoint2.Zero),
                "The torso should be fully healed (floored at 0, not negative).");
            Assert.That(mobDamageAfter, Is.EqualTo(FixedPoint2.New(10)),
                "The mob's total should be exactly the leg's untouched 10 - not less. The old bug subtracted the full nominal 6 from the mob regardless of what the torso actually absorbed, landing the mob at 6 instead of the real 10.");
        });
    }
}

using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Medical;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Tourniquet;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// User request: "add an information when tourniquet applied to the health analyzer." Bleeding
/// status was already fully wired (HealthAnalyzerSystem.FetchBleedData, HealthAnalyzerWindow's
/// Conditions list rendering condition-body-bleeding-*) - this adds the same for tourniquets,
/// keying off TourniquetSystem's own "TourniquetPresent" bleed-modifier identifier instead of a
/// new component (see FetchTourniquetData's own doc comment).
///
/// FetchTourniquetData is private, invoked via reflection (same pattern as AfkSystemTest) rather
/// than simulating the full BUI-open + scan-DoAfter flow, since the data-fetch logic itself is
/// what's being tested, not the analyzer's scan machinery (which FetchBleedData already
/// exercises identically and untested, out of scope to add now).
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class HealthAnalyzerTourniquetTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> PiercingDamageType = "Piercing";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HealthAnalyzerTourniquetSelf
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: Consciousness
    threshold: 95
    cap: 190
  - type: Targeting

- type: entity
  id: HealthAnalyzerTourniquetArm
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
  id: HealthAnalyzerTourniquetLeg
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
";

    [Test]
    public async Task FetchTourniquetDataReportsOnlyTheTourniquetedLimb()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sHealthAnalyzer = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<HealthAnalyzerSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid self = default;
        EntityUid arm = default;
        EntityUid leg = default;
        BodyComponent bodyComp = default!;

        await server.WaitPost(() =>
        {
            self = sEntMan.SpawnEntity("HealthAnalyzerTourniquetSelf", coords);
            arm = sEntMan.SpawnEntity("HealthAnalyzerTourniquetArm", coords);
            leg = sEntMan.SpawnEntity("HealthAnalyzerTourniquetLeg", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(self, BodyComponent.ContainerID);
            container.Insert(arm, organsContainer);
            container.Insert(leg, organsContainer);

            sEntMan.GetComponent<TargetingComponent>(self).Target = TargetBodyPart.LeftArm;
            bodyComp = sEntMan.GetComponent<BodyComponent>(self);
        });

        await pair.RunTicksSync(5);

        // Wound both limbs so there's a wound to carry the tourniquet's bleed modifier.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            sDamageable.TryChangeDamage(arm, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: self);
            sDamageable.TryChangeDamage(leg, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: self);
        });

        await pair.RunTicksSync(5);

        var fetchMethod = typeof(HealthAnalyzerSystem).GetMethod("FetchTourniquetData", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await server.WaitAssertion(() =>
        {
            var before = (Dictionary<TargetBodyPart, bool>) fetchMethod.Invoke(sHealthAnalyzer, new object[] { bodyComp })!;
            Assert.That(before[TargetBodyPart.LeftArm], Is.False, "Arm shouldn't show as tourniqueted before one is applied.");
            Assert.That(before[TargetBodyPart.LeftLeg], Is.False, "Leg shouldn't show as tourniqueted at all - it was never targeted.");
        });

        // Apply the tourniquet to the arm (raising TourniquetDoAfterEvent directly, same
        // established pattern as TourniquetTest.cs).
        await server.WaitPost(() =>
        {
            var tourniquet = sEntMan.SpawnEntity("Tourniquet", coords);
            var doAfterArgs = new DoAfterArgs(sEntMan, self, TimeSpan.FromSeconds(1), new TourniquetDoAfterEvent("ArmLeft"), self, target: self, used: tourniquet);
            var ev = new TourniquetDoAfterEvent("ArmLeft")
            {
                DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero),
            };
            sEntMan.EventBus.RaiseLocalEvent(self, ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var after = (Dictionary<TargetBodyPart, bool>) fetchMethod.Invoke(sHealthAnalyzer, new object[] { bodyComp })!;
            Assert.That(after[TargetBodyPart.LeftArm], Is.True, "The tourniqueted arm should now show as tourniqueted.");
            Assert.That(after[TargetBodyPart.LeftLeg], Is.False, "The untouched leg should still show as not tourniqueted.");
        });
    }
}

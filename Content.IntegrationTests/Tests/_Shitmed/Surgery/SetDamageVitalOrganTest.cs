using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

/// <summary>
/// Found while investigating a production log (2026-07-16): SetDamage's unconditional
/// TryHaltAllBleeding(part, force: true) call at the top assumed `part` is always a limb organ
/// (which has WoundableComponent), but organ-specific surgeries (inserting/sealing a Heart/Eyes/
/// Ears/Tongue) pass the vital organ itself as `part` - those never have WoundableComponent, so
/// every such surgery step damage/heal logged "can't resolve WoundableComponent" (confirmed
/// ~35 occurrences in the real log, category system.wound, stack trace through
/// SetDamage -> TryHaltAllBleeding). Fixed by gating the call on HasComp&lt;WoundableComponent&gt;.
///
/// This integration test harness treats any unexpected [ERRO] log as a test failure - so this
/// test doesn't need to assert the log directly; simply exercising SetDamage on a non-woundable
/// organ without the harness flagging an error IS the regression guard (it would have failed
/// before this fix).
/// </summary>
[TestFixture]
[TestOf(typeof(SurgerySystem))]
public sealed class SetDamageVitalOrganTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SetDamageVitalOrganTestBody
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: SurgeryTarget

- type: entity
  id: SetDamageVitalOrganTestHeart
  components:
  - type: Organ
    category: Heart
  - type: Damageable
  - type: OrganIntegrity
    integrityCap: 17
    integrityThresholds:
      Normal: 17
      Damaged: 9
      Destroyed: 0
";

    [Test]
    public async Task DealingSurgeryDamageToAVitalOrganDoesNotLogAResolveError()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid body = default;
        EntityUid heart = default;
        EntityUid user = default;

        await server.WaitPost(() =>
        {
            body = sEntMan.SpawnEntity("SetDamageVitalOrganTestBody", coords);
            heart = sEntMan.SpawnEntity("SetDamageVitalOrganTestHeart", coords);
            user = sEntMan.SpawnEntity(null, coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(body, BodyComponent.ContainerID);
            container.Insert(heart, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            var damage = new DamageSpecifier(proto, FixedPoint2.New(5));
            var ev = new SurgeryStepDamageEvent(user, body, heart, heart, damage, 1f);
            sEntMan.EventBus.RaiseLocalEvent(body, ref ev);
        });

        await pair.RunTicksSync(5);

        // Manually verified (by temporarily reverting the fix and re-running) that without the
        // HasComp<WoundableComponent> guard, this exact sequence reproduces the production
        // stack trace verbatim: WoundSystem.TryHaltAllBleeding -> SurgerySystem.SetDamage ->
        // SurgerySystem.OnSurgeryStepDamage, "Can't resolve WoundableComponent" - and that the
        // harness's own "any unexpected [ERRO] fails the test" behavior does catch it. With the
        // fix in place, no such error fires. If it throws/logs here, the regression has returned.
        Assert.Pass();
    }
}

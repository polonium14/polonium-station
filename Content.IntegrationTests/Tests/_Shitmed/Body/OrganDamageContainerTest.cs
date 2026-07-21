using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// User (via VV) found 120 Structural damage sitting on a torso organ's DamageableComponent
/// after being hit with a fire axe - a completely normal vanilla weapon whose Structural
/// component exists so it can also chop through walls/airlocks (Resources/Prototypes/Entities/
/// Objects/Weapons/Melee/fireaxe.yml's own comment: "axes are kinda like sharp hammers, you
/// know?"). Root cause: vanilla's MobDamageable (Resources/Prototypes/Entities/Mobs/base.yml)
/// sets `damageContainer: Biological` on the MOB's own Injurable component, which is what
/// makes DamageableSystem.OnDamageDealt's SupportsType check silently reject non-biological
/// types like Structural for a person. But human.yml's per-limb organs (added for Shitmed's
/// per-limb wound tracking) never set that field on their OWN Injurable components - each is a
/// separate entity, so it doesn't inherit the mob's restriction - defaulting to
/// InjurableComponent.DamageContainer == null, which SupportsType treats as "accepts
/// everything." Structural damage sailed straight onto the organ, invisible everywhere (no UI
/// renders it on a person) since nothing was ever supposed to be able to deal it to one. Fixed
/// by adding `damageContainer: Biological` to every limb-organ's Injurable component, matching
/// the mob's own restriction.
/// </summary>
[TestFixture]
[TestOf(typeof(DamageableSystem))]
public sealed class OrganDamageContainerTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> StructuralDamageType = "Structural";
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OrganDamageContainerTestTorsoOrgan
  components:
  - type: Damageable
  - type: Injurable
    damageContainer: Biological
";

    [Test]
    public async Task OrganRejectsStructuralDamageButAcceptsBlunt()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid organ = default;

        await server.WaitPost(() =>
        {
            organ = sEntMan.SpawnEntity("OrganDamageContainerTestTorsoOrgan", coords);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var structuralProto = sProtoMan.Index(StructuralDamageType);
            sDamageable.TryChangeDamage(organ, new DamageSpecifier(structuralProto, FixedPoint2.New(120)), interruptsDoAfters: false);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            Assert.That(sDamageable.GetTotalDamage(organ), Is.EqualTo(FixedPoint2.Zero),
                "A Biological-container organ should reject Structural damage entirely, same as the mob it belongs to.");
#pragma warning restore CS0618
        });

        await server.WaitPost(() =>
        {
            var bluntProto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(organ, new DamageSpecifier(bluntProto, FixedPoint2.New(10)), interruptsDoAfters: false);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            Assert.That(sDamageable.GetTotalDamage(organ), Is.EqualTo(FixedPoint2.New(10)),
                "Sanity check: a supported type (Blunt, part of the Brute group) should still land normally.");
#pragma warning restore CS0618
        });
    }
}

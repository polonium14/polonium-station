using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
public sealed class WoundableVisualsAppearanceTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [Test]
    public async Task WoundableAppearanceDataTracksWoundListAsWoundsAreAddedAndHealed()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sAppearance = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedAppearanceSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid human = default;
        await server.WaitPost(() =>
        {
            human = sEntMan.SpawnEntity("MobHuman", coords);
            // Barotrauma routes through every limb-organ now (2026-07-16: no mob-only damage) -
            // remove it so passive pressure damage on this bare test map can't add a stray wound
            // and break the "fresh organ has no wounds" / "fully healed has no wounds" checks
            // below.
            sEntMan.RemoveComponent<BarotraumaComponent>(human);
        });
        await pair.RunTicksSync(10);

        EntityUid torsoOrgan = default;
        await server.WaitAssertion(() =>
        {
            var body = sEntMan.GetComponent<BodyComponent>(human);
            torsoOrgan = body.Organs!.ContainedEntities.First(o =>
                sEntMan.TryGetComponent<OrganComponent>(o, out var organ) && organ.Category == "Torso");

            // No wounds yet - appearance data should either be absent or an empty list.
            var hadDataBefore = sAppearance.TryGetData<WoundVisualizerGroupData>(torsoOrgan, WoundableVisualizerKeys.Wounds, out var before);
            Assert.That(!hadDataBefore || before.GroupList.Count == 0, "Fresh organ shouldn't report any wounds in its appearance data.");
        });

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(torsoOrgan, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: null);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sAppearance.TryGetData<WoundVisualizerGroupData>(torsoOrgan, WoundableVisualizerKeys.Wounds, out var afterDamage), Is.True,
                "Dealing damage should have created a wound and pushed it into the appearance data.");
            Assert.That(afterDamage.GroupList, Is.Not.Empty, "The wound list pushed to appearance data should contain the new wound.");
        });

        await server.WaitPost(() => { sWound.ForceHealWoundsOnWoundable(torsoOrgan, out _); });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sAppearance.TryGetData<WoundVisualizerGroupData>(torsoOrgan, WoundableVisualizerKeys.Wounds, out var afterHeal), Is.True);
            Assert.That(afterHeal.GroupList, Is.Empty, "Fully healing the wound should remove it from the appearance data's wound list.");
        });
    }
}

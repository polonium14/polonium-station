// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
public sealed class ChemHealAfterTorsoZeroTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [Test]
    public async Task UntargetedChemHealStillReachesAnArmOnceTorsoIsAtZero()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("MobHuman", coords);
            sEntMan.RemoveComponent<BarotraumaComponent>(victim);
        });

        await pair.RunTicksSync(10);

        EntityUid torso = default, arm = default;
        await server.WaitAssertion(() =>
        {
            var body = sEntMan.GetComponent<BodyComponent>(victim);
            torso = body.Organs!.ContainedEntities.First(o =>
                sEntMan.TryGetComponent<OrganComponent>(o, out var organ) && organ.Category == "Torso");
            arm = body.Organs!.ContainedEntities.First(o =>
                sEntMan.TryGetComponent<OrganComponent>(o, out var organ) && organ.Category == "ArmLeft");
        });

        var proto = sProtoMan.Index(BluntDamageType);

        await server.WaitPost(() =>
        {
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(60)), ignoreResistances: true, interruptsDoAfters: false);
        });

        await pair.RunTicksSync(5);

        FixedPoint2 torsoBefore = default, armBefore = default, mobBefore = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            torsoBefore = sDamageable.GetTotalDamage(torso);
            armBefore = sDamageable.GetTotalDamage(arm);
            mobBefore = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618
            TestContext.Out.WriteLine($"[DEBUG] after untargeted hit: torso={torsoBefore} arm={armBefore} mob={mobBefore}");
            Assert.That(torsoBefore, Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(armBefore, Is.GreaterThan(FixedPoint2.Zero));
        });

        await server.WaitPost(() =>
        {
            sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(-999)), ignoreResistances: true, interruptsDoAfters: false);
        });

        await pair.RunTicksSync(5);

        FixedPoint2 torsoAfterTargeted = default, armAfterTargeted = default, mobAfterTargeted = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            torsoAfterTargeted = sDamageable.GetTotalDamage(torso);
            armAfterTargeted = sDamageable.GetTotalDamage(arm);
            mobAfterTargeted = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618
            TestContext.Out.WriteLine($"[DEBUG] after targeted torso heal: torso={torsoAfterTargeted} arm={armAfterTargeted} mob={mobAfterTargeted}");

            Assert.That(torsoAfterTargeted, Is.EqualTo(FixedPoint2.Zero), "Sanity check: the torso should be fully healed now.");
            Assert.That(armAfterTargeted, Is.EqualTo(armBefore), "Sanity check: healing the torso directly shouldn't touch the arm.");
        });

        await server.WaitPost(() =>
        {
            var effect = new EvenHealthChange
            {
                Damage = new() { ["Brute"] = FixedPoint2.New(-20) },
            };
            var ev = new EntityEffectEvent<EvenHealthChange>(effect, 1f, null);
            sEntMan.EventBus.RaiseLocalEvent(victim, ref ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var armAfterChem = sDamageable.GetTotalDamage(arm);
            var mobAfterChem = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618
            TestContext.Out.WriteLine($"[DEBUG] after EvenHealthChange (Bicaridine-shaped): arm={armAfterChem} mob={mobAfterChem}");

            Assert.That(armAfterChem, Is.LessThan(armAfterTargeted),
                "The arm still had blunt damage - the Bicaridine-shaped EvenHealthChange effect should have reduced it " +
                "same as it would with an undamaged torso. If this fails, an already-healed torso is still suppressing the heal " +
                "reaching other limbs.");
        });
    }
}

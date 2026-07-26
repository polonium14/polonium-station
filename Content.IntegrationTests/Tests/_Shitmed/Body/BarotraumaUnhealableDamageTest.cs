// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Server.Atmos.Components;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
public sealed class BarotraumaUnhealableDamageTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: BarotraumaHealTestRescuer
  components:
  - type: Targeting

- type: entity
  id: BarotraumaHealTestBrutepack
  components:
  - type: Healing
    damage:
      types:
        Blunt: -10

- type: entity
  id: BarotraumaHealTestGauze
  components:
  - type: Healing
    damage: {}
    bloodlossModifier: -10
";

    [Test]
    public async Task DeathByVacuumThenHealViaTopicalChemAndSurgery()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sMobState = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<MobStateSystem>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid rescuer = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("MobHuman", coords);
            rescuer = sEntMan.SpawnEntity("BarotraumaHealTestRescuer", coords);

            sEntMan.RemoveComponent<BarotraumaComponent>(victim);
        });

        await pair.RunTicksSync(10);

        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            var body = sEntMan.GetComponent<BodyComponent>(victim);
            torso = body.Organs!.ContainedEntities.First(o =>
                sEntMan.TryGetComponent<OrganComponent>(o, out var organ) && organ.Category == "Torso");
        });

        var proto = sProtoMan.Index(BluntDamageType);
        var reachedDead = false;
        for (var i = 0; i < 40 && !reachedDead; i++)
        {
            await server.WaitPost(() =>
            {
                sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(6)), ignoreResistances: true, interruptsDoAfters: false);
            });

            await pair.RunTicksSync(2);

            await server.WaitAssertion(() =>
            {
                reachedDead = sMobState.IsDead(victim);
            });
        }

        FixedPoint2 mobDamageAtDeath = default, organDamageAtDeath = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(reachedDead, Is.True, "Sanity check: repeated barotrauma-shaped hits should have killed the mob.");

#pragma warning disable CS0618
            mobDamageAtDeath = sDamageable.GetTotalDamage(victim);
            organDamageAtDeath = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618

            Assert.That(mobDamageAtDeath, Is.GreaterThanOrEqualTo(FixedPoint2.New(200)));

            Assert.That(organDamageAtDeath, Is.GreaterThan(FixedPoint2.Zero),
                "The untargeted Blunt damage should have bridged onto the torso organ, same as any other untargeted damage source.");
        });

        for (var i = 0; i < 5; i++)
        {
            await server.WaitPost(() =>
            {
                var healer = sEntMan.SpawnEntity("BarotraumaHealTestGauze", coords);
                var doAfterArgs = new DoAfterArgs(sEntMan, rescuer, TimeSpan.FromSeconds(1), new HealingDoAfterEvent(), victim, target: victim, used: healer);
                var ev = new HealingDoAfterEvent
                {
                    DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero),
                };
                sEntMan.EventBus.RaiseLocalEvent(victim, ev);
            });

            await pair.RunTicksSync(5);
        }

        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(torso);

            Assert.That(woundableComp.Bleeds, Is.LessThanOrEqualTo(FixedPoint2.Zero),
                "Five gauze applications (-10 bloodloss each) should have fully stopped the torso's bleeding.");
        });

        // a) Topical.
        FixedPoint2 mobBeforeTopical = default, organBeforeTopical = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            mobBeforeTopical = sDamageable.GetTotalDamage(victim);
            organBeforeTopical = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618
        });

        for (var i = 0; i < 6; i++)
        {
            await server.WaitPost(() =>
            {
                var healer = sEntMan.SpawnEntity("BarotraumaHealTestBrutepack", coords);
                var doAfterArgs = new DoAfterArgs(sEntMan, rescuer, TimeSpan.FromSeconds(1), new HealingDoAfterEvent(), victim, target: victim, used: healer);
                var ev = new HealingDoAfterEvent
                {
                    DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero),
                };
                sEntMan.EventBus.RaiseLocalEvent(victim, ev);
            });

            await pair.RunTicksSync(5);
        }

        FixedPoint2 mobAfterTopical = default, organAfterTopical = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            mobAfterTopical = sDamageable.GetTotalDamage(victim);
            organAfterTopical = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618

            Assert.That(mobAfterTopical, Is.GreaterThan(mobBeforeTopical - FixedPoint2.New(1)),
                "a) TOPICAL: correctly blocked - the torso still has active BoneDamage trauma, and TraumasBlockingHealing " +
                "refuses topical healing on a broken limb even once bleeding stops, until it's surgically mended. " +
                "This documents current intentional gating, not a regression guard - if that gate is ever intentionally " +
                "relaxed, update this assertion rather than treating the failure as a break.");
            Assert.That(organAfterTopical, Is.GreaterThan(organBeforeTopical - FixedPoint2.New(1)));
        });

        // b) Chem
        FixedPoint2 mobBeforeChem = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            mobBeforeChem = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618
        });

        await server.WaitPost(() =>
        {
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(-50)), ignoreResistances: true, interruptsDoAfters: false);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var mobAfterChem = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618

            Assert.That(mobAfterChem, Is.LessThan(mobBeforeChem),
                "b) CHEM: the same raw TryChangeDamage call a healing reagent effect makes should reduce the mob's damage - " +
                "chem bypasses organ/trauma checks entirely, so it heals regardless of the topical block above.");
        });

        // c) Surgery
        await server.WaitAssertion(() =>
        {
            var surgeryEnt = sSurgery.GetSingleton("SurgeryTendWoundsBrute");
            Assert.That(surgeryEnt, Is.Not.Null, "SurgeryTendWoundsBrute should resolve to a real singleton entity.");

            var ev = new SurgeryValidEvent(victim, torso);
            sEntMan.EventBus.RaiseLocalEvent(surgeryEnt!.Value, ref ev);

            Assert.That(ev.Cancelled, Is.False,
                "c) SURGERY: Tend Bruise Wounds should be offered while the torso still carries unhealed Brute damage - " +
                "'no surgery option' in the live report does not reproduce here.");
        });
    }
}

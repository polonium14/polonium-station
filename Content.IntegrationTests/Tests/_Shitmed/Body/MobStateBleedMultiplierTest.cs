// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
[TestOf(typeof(SharedBloodstreamSystem))]
public sealed class MobStateBleedMultiplierTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    private const float StartingBleedAmount = 5f;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: BleedMultiplierTestMob
  components:
  - type: Damageable
  - type: Injurable
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: MobState
  - type: MobThresholds
    thresholds:
      0: Alive
      50: Critical
      100: Dead
";

    [Test]
    public async Task CritAndDeadBodiesBleedOutSlowerButStillClotAtFullSpeed()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSystems = server.ResolveDependency<IEntitySystemManager>();
        var sBloodstream = sSystems.GetEntitySystem<SharedBloodstreamSystem>();
        var sDamageable = sSystems.GetEntitySystem<DamageableSystem>();
        var sMobState = sSystems.GetEntitySystem<MobStateSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid alive = default;
        EntityUid crit = default;
        EntityUid dead = default;

        await server.WaitPost(() =>
        {
            alive = sEntMan.SpawnEntity("BleedMultiplierTestMob", coords);
            crit = sEntMan.SpawnEntity("BleedMultiplierTestMob", coords);
            dead = sEntMan.SpawnEntity("BleedMultiplierTestMob", coords);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(crit, new DamageSpecifier(proto, FixedPoint2.New(60)), ignoreResistances: true);
            sDamageable.TryChangeDamage(dead, new DamageSpecifier(proto, FixedPoint2.New(120)), ignoreResistances: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sMobState.IsAlive(alive), Is.True, "Setup: the undamaged mob should still be alive.");
                Assert.That(sMobState.IsCritical(crit), Is.True, "Setup: 60 damage should have put the second mob into crit.");
                Assert.That(sMobState.IsDead(dead), Is.True, "Setup: 120 damage should have killed the third mob.");
            });
        });

        var lost = new Dictionary<EntityUid, float>();
        var bleedBefore = new Dictionary<EntityUid, float>();
        var bleedAfter = new Dictionary<EntityUid, float>();

        await server.WaitPost(() =>
        {
            foreach (var mob in new[] { alive, crit, dead })
            {
                var comp = sEntMan.GetComponent<BloodstreamComponent>(mob);

                sBloodstream.TryModifyBleedAmount(mob, -comp.MaxBleedAmount);
                sBloodstream.TryModifyBleedAmount(mob, StartingBleedAmount);

                bleedBefore[mob] = comp.BleedAmount;

                var before = sBloodstream.GetBloodLevel(mob);
                sBloodstream.TickBleed((mob, comp));

                lost[mob] = before - sBloodstream.GetBloodLevel(mob);
                bleedAfter[mob] = comp.BleedAmount;
            }
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(lost[alive], Is.GreaterThan(0f),
                "Sanity: a living mob with a real bleed rate should lose blood on a TickBleed.");

            Assert.Multiple(() =>
            {
                Assert.That(bleedBefore[crit], Is.EqualTo(bleedBefore[alive]).Within(0.001f),
                    "Sanity: all three mobs must enter the tick at the same bleed rate, or the deltas below compare nothing.");
                Assert.That(bleedBefore[dead], Is.EqualTo(bleedBefore[alive]).Within(0.001f),
                    "Sanity: all three mobs must enter the tick at the same bleed rate, or the deltas below compare nothing.");
            });

            Assert.Multiple(() =>
            {
                Assert.That(lost[crit], Is.EqualTo(lost[alive] * 0.75f).Within(1).Percent,
                    "A mob in crit should lose blood at three quarters the rate of a living one.");

                Assert.That(lost[dead], Is.EqualTo(lost[alive] * 0.2f).Within(1).Percent,
                    "A dead mob should lose blood at a fifth of the rate of a living one.");

                Assert.That(bleedAfter[crit], Is.EqualTo(bleedAfter[alive]).Within(0.001f),
                    "Crit should not slow down clotting - only the blood actually lost is scaled.");

                Assert.That(bleedAfter[dead], Is.EqualTo(bleedAfter[alive]).Within(0.001f),
                    "Death should not slow down clotting - only the blood actually lost is scaled.");
            });
        });
    }
}

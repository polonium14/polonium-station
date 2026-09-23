// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
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
/// Live server crash report: dealing with a real patient's Brute/Burn wound via the "Tend
/// Bruise/Burn Wounds" surgery threw a KeyNotFoundException out of OnTendWoundsStep
/// (SharedSurgerySystem.Steps.cs:217) every time the "Repair damaged/burnt tissue" step was
/// used - the DoAfter completion swallowed and logged the exception rather than surfacing it,
/// so the tool-use animation still looked successful while nothing was ever healed. Root
/// cause: SurgeryStepRepairBruteTissue/SurgeryStepRepairBurnTissue's SurgeryTendWoundsEffect.
/// Damage field was authored as `damage: groups: {Brute: -15}` in surgery_steps.yml -
/// DamageSpecifier (Content.Shared/Damage/DamageSpecifier.cs) only has a `types` DataField,
/// no `groups` field at all, so that YAML key was silently ignored and Damage.DamageDict
/// stayed empty. OnTendWoundsStep then does `adjustedDamage.DamageDict[type] -= bonus` for
/// every type in the resolved damage group (Blunt/Slash/Piercing for Brute, Heat/Shock/Cold/
/// Caustic for Burn) - reading a dictionary indexer that was never populated throws
/// KeyNotFoundException immediately, before any heal damage event is ever raised. This
/// mechanism appears to have never worked since it was authored, unrelated to any of this
/// session's earlier damage-bridge/mob-sync work (confirmed no C# in the call chain was
/// touched this session prior to this fix). Fixed by rewriting both steps' `damage:` blocks
/// to `types:` with each real damage type seeded at 0, giving OnTendWoundsStep's per-type
/// subtraction real keys to work with.
/// </summary>
[TestFixture]
public sealed class TendWoundsStepTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TendWoundsStepTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: SurgeryTarget

- type: entity
  id: TendWoundsStepTestTorsoOrgan
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

- type: entity
  id: TendWoundsStepTestArmOrgan
  components:
  - type: Organ
    category: ArmLeft
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 80
    healAbility: 0
    thresholds:
      Healthy: 80
      Minor: 64
      Moderate: 48
      Severe: 32
      Critical: 16
      Mangled: 6
      Severed: 0
";

    private static readonly ProtoId<DamageTypePrototype> HeatDamageType = "Heat";

    [TestCase("Blunt", "SurgeryTendWoundsBrute", "SurgeryStepRepairBruteTissue", false)]
    [TestCase("Heat", "SurgeryTendWoundsBurn", "SurgeryStepRepairBurnTissue", false)]
    [TestCase("Blunt", "SurgeryTendWoundsBrute", "SurgeryStepRepairBruteTissue", true)]
    public async Task FinishedTendingStillAllowsSealing(string damageType, string surgeryId, string repairId, bool blocked)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
            var torso = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var cautery = SEntMan.SpawnEntity("Cautery", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(torso, containers.GetContainer(body, BodyComponent.ContainerID));
            var surgery = SEntMan.System<SurgerySystem>();
            var damage = SEntMan.System<DamageableSystem>();
            var wounds = SEntMan.System<WoundSystem>();
            var proto = SProtoMan.Index<DamageTypePrototype>(damageType);
            damage.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(5)), ignoreResistances: true);
            SEntMan.AddComponent<IncisionOpenComponent>(torso);

            if (blocked)
            {
                var wound = wounds.GetWoundableWounds(torso).Single();
                var woundable = SEntMan.GetComponent<WoundableComponent>(torso);
                var inflicter = SEntMan.GetComponent<TraumaInflicterComponent>(wound);
                SEntMan.System<TraumaSystem>().AddTrauma(torso, (torso, woundable), (wound, inflicter),
                    TraumaType.Dismemberment, FixedPoint2.New(5));
            }

            // Damage can disappear during surgery, including while a dismemberment wound
            // still requires its own treatment. Neither case should trap the incision open.
            damage.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(-5)), ignoreResistances: true);
            var surgeryEnt = surgery.GetSingleton(surgeryId)!.Value;
            var valid = new SurgeryValidEvent(body, torso, Category: "Torso");
            SEntMan.EventBus.RaiseLocalEvent(surgeryEnt, ref valid);
            Assert.That(valid.Cancelled, Is.False);
            Assert.That(surgery.IsStepComplete(body, torso, repairId, surgeryEnt), Is.True);
            Assert.That(surgery.GetNextStep(body, torso, surgeryEnt, body)!.Value.Step, Is.EqualTo(2));

            var seal = surgery.GetSingleton("SurgeryStepSealTendWound")!.Value;
            var ev = new SurgeryStepEvent(body, body, torso, cautery, surgeryEnt, seal);
            SEntMan.EventBus.RaiseLocalEvent(seal, ref ev);
            Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(torso), Is.False);
            valid = new SurgeryValidEvent(body, torso, Category: "Torso");
            SEntMan.EventBus.RaiseLocalEvent(surgeryEnt, ref valid);
            Assert.That(valid.Cancelled, Is.True, "A finished incision must not keep offering a no-op tend surgery.");
        });
    }

    [Test]
    public async Task RepairBruteTissueStepHealsWithoutThrowing()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
            torso = sEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
        });

        await pair.RunTicksSync(5);

        // Deal real Blunt (Brute-group) damage so there's an actual wound to tend - kept below
        // WoundBlunt's own BleedInflicter.severityThreshold (8, wounds.yml) so the wound stays
        // healable (an actively-bleeding wound correctly blocks CanHealWound - a separate,
        // pre-existing, working mechanism this test isn't exercising).
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(5)));
        });

        await pair.RunTicksSync(5);

        FixedPoint2 organBefore = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            organBefore = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618
            Assert.That(organBefore, Is.GreaterThan(FixedPoint2.Zero), "Sanity check: the organ should actually be damaged before tending it.");
        });

        // Raises SurgeryStepEvent on the real step singleton, exactly as OnTargetDoAfter does
        // once the "Repair damaged tissue" tool-use DoAfter completes.
        await server.WaitAssertion(() =>
        {
            var stepEnt = sSurgery.GetSingleton("SurgeryStepRepairBruteTissue");
            Assert.That(stepEnt, Is.Not.Null, "SurgeryStepRepairBruteTissue should resolve to a real singleton entity.");

            var surgeryEnt = sSurgery.GetSingleton("SurgeryTendWoundsBrute");
            Assert.That(surgeryEnt, Is.Not.Null, "SurgeryTendWoundsBrute should resolve to a real singleton entity.");

            var ev = new SurgeryStepEvent(victim, victim, torso, EntityUid.Invalid, surgeryEnt!.Value, stepEnt!.Value);
            Assert.DoesNotThrow(() => sEntMan.EventBus.RaiseLocalEvent(stepEnt!.Value, ref ev),
                "OnTendWoundsStep previously threw KeyNotFoundException here for both Brute and Burn - the live server crash this test guards against.");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var organAfter = sDamageable.GetTotalDamage(torso);
#pragma warning restore CS0618
            Assert.That(organAfter, Is.LessThan(organBefore),
                "The tend-wounds step should have actually healed some of the organ's damage, not just avoided crashing.");
        });
    }

    /// <summary>
    /// Live playtest report: "so how is it possible that i can see sum 0 when only chest is
    /// healed and every other body part is at 100+" - confirmed all Heat/Burn damage. Repeated
    /// clicks of "Repair burnt tissue" (SurgeryRepeatableStep) fully heal a torso's Heat wound
    /// via many small OnTendWoundsStep calls (the bonus shrinks toward zero as the torso's own
    /// wound heals, matching the real gradual-decline pattern from the server log that led to
    /// this test). This reproduces that exact sequence with a SECOND organ (an arm) also
    /// carrying its own, completely separate Heat damage, checking the mob's total after EVERY
    /// single repair click - not just before/after - to catch a transient over-correction that
    /// a single before/after snapshot could miss.
    /// </summary>
    [Test]
    public async Task RepeatedTendWoundsClicksOnOneOrganNeverUndercutAnotherOrgansSameTypeDamage()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid torso = default;
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
            torso = sEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            arm = sEntMan.SpawnEntity("TendWoundsStepTestArmOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
            container.Insert(arm, organsContainer);
        });

        await pair.RunTicksSync(5);

        // Both organs take real Heat damage - the arm's is never touched by anything that
        // follows and must survive completely intact throughout.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(HeatDamageType);
            sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(20)));
            sDamageable.TryChangeDamage(arm, new DamageSpecifier(proto, FixedPoint2.New(15)));
        });

        await pair.RunTicksSync(5);

        FixedPoint2 armDamageBaseline = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            armDamageBaseline = sDamageable.GetTotalDamage(arm);
            var mobTotal = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618
            Assert.That(armDamageBaseline, Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(mobTotal, Is.EqualTo(FixedPoint2.New(35)), "Sanity check: mob total should be torso's 20 plus arm's 15.");
        });

        var stepEnt = default(EntityUid?);
        var surgeryEnt = default(EntityUid?);
        await server.WaitAssertion(() =>
        {
            stepEnt = sSurgery.GetSingleton("SurgeryStepRepairBurnTissue");
            surgeryEnt = sSurgery.GetSingleton("SurgeryTendWoundsBurn");
            Assert.That(stepEnt, Is.Not.Null);
            Assert.That(surgeryEnt, Is.Not.Null);
        });

        // Click "Repair burnt tissue" on the torso repeatedly - SurgeryRepeatableStep's real
        // behavior - until the torso's own Heat wound is fully gone. Checks the arm's damage
        // and the mob's total after EVERY click, not just at the end.
        for (var i = 0; i < 500; i++)
        {
            await server.WaitPost(() =>
            {
                var ev = new SurgeryStepEvent(victim, victim, torso, EntityUid.Invalid, surgeryEnt!.Value, stepEnt!.Value);
                sEntMan.EventBus.RaiseLocalEvent(stepEnt!.Value, ref ev);
            });

            await pair.RunTicksSync(1);

            var stillHealing = false;
            await server.WaitAssertion(() =>
            {
#pragma warning disable CS0618
                var armDamageNow = sDamageable.GetTotalDamage(arm);
                var torsoDamageNow = sDamageable.GetTotalDamage(torso);
                var mobTotalNow = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618

                Assert.That(armDamageNow, Is.EqualTo(armDamageBaseline),
                    $"[click {i}] The arm's own Heat damage should never move just from tending the torso's wound.");
                Assert.That(mobTotalNow, Is.EqualTo(torsoDamageNow + armDamageBaseline),
                    $"[click {i}] Mob total should always equal torso's current damage plus the arm's untouched 15 - the exact 'sum 0 while other parts are still hurt' bug this test guards against.");

                stillHealing = torsoDamageNow > FixedPoint2.Zero;
            });

            if (!stillHealing)
                break;
        }

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var torsoDamageFinal = sDamageable.GetTotalDamage(torso);
            var armDamageFinal = sDamageable.GetTotalDamage(arm);
            var mobTotalFinal = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618

            // The heal-per-click is proportional to the torso's own remaining severity (a
            // shrinking geometric decay), and can stall well short of zero if a trauma gets
            // probabilistically induced on the wound and blocks further healing (separate,
            // real finding - not what this test is checking). What matters, verified every
            // single iteration above AND here again: the arm never moves, and the mob total
            // always equals torso's current damage plus the arm's untouched 15 - never
            // independently drops to 0.
            Assert.That(armDamageFinal, Is.EqualTo(FixedPoint2.New(15)), "The arm's damage must still be exactly its original 15.");
            Assert.That(mobTotalFinal, Is.EqualTo(torsoDamageFinal + FixedPoint2.New(15)),
                "The mob's total should always be torso's current damage plus the arm's untouched 15 - never independently drops to 0.");
        });
    }
}

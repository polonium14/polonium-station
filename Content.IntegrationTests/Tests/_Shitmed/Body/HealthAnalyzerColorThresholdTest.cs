using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.Components;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// Verifies the 2026-07-16 rescale of human.yml's limb-organ Woundable thresholds. Went through
/// three designs: first a flat integrityCap 100 on every limb (matching master's flat, part-
/// agnostic BodyPartComponent.IntegrityThresholds table directly); then a narrower fallback per
/// user direction after playtesting the flat version - kept each limb's original per-part-size
/// integrityCap (Torso 200, Head 125, Arm/Leg 80, Hand/Foot 60, Groin 100, preserving the
/// original max-damage-before-death scale) but only pulled the Minor threshold to cap-10;
/// then extended per further user feedback ("what about the other caps, they seem off too") to
/// pull every remaining bucket to the same cap-master's-raw-value formula (Moderate cap-20,
/// Severe cap-40, Critical cap-60, Mangled cap-75, Severed cap-90 - previously always 0). Net
/// effect: every limb reaches its worst health-analyzer color state at the same 90 raw damage
/// as master, regardless of size - the cap only controls how much further damage a part can
/// absorb beyond that before whatever's next (death/dismemberment), not when its color first
/// starts changing. Uses the real MobHuman prototype so this catches any future per-species
/// content drift, not just a synthetic fixture.
/// </summary>
[TestFixture]
[TestOf(typeof(WoundSystem))]
public sealed class HealthAnalyzerColorThresholdTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [Test]
    public async Task TorsoColorThresholdsMatchMastersRawDamageFeelAtEveryBucket()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid human = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            human = sEntMan.SpawnEntity("MobHuman", coords);
            // Barotrauma now routes through BodyDamageBridgeSystem like every other
            // environmental source (2026-07-16: no mob-only damage) - a bare MobHuman on this
            // unpressurized test map would otherwise take passive pressure damage straight onto
            // the same torso organ this test measures, drifting the cumulative totals below off
            // schedule. Removing BarotraumaComponent entirely keeps this test isolated to only
            // the damage it deals itself.
            sEntMan.RemoveComponent<BarotraumaComponent>(human);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var body = sEntMan.GetComponent<BodyComponent>(human);
            torso = LimbTargetMap.TryGetOrganByCategory(sEntMan, body, "Torso", out var organ) ? organ : default;
            Assert.That(torso, Is.Not.EqualTo(default(EntityUid)));

            var statesBefore = sWound.GetDamageableStatesOnBody(human);
            Assert.That(statesBefore[TargetBodyPart.Chest], Is.EqualTo(WoundableSeverity.Healthy),
                "Undamaged torso should read Healthy, matching master's blue state.");
        });

        var proto = sProtoMan.Index(BluntDamageType);

        // (damage to deal this step, cumulative total after dealing it, expected severity at
        // that total) - walks every bucket boundary in one pass, checking one point below the
        // threshold stays in the previous bucket before crossing it, mirroring master's raw
        // 10/20/40/60/75/90 table exactly. Stops at Mangled, not Severed: GetDamageableStatesOnBody
        // explicitly skips WoundableSeverity.Severed in its own threshold scan (matches
        // CheckWoundableSeverityThresholds, the real gameplay severity setter, doing the same) -
        // Severed is only ever reached via an explicit DestroyWoundable/AmputateWoundableSafely
        // call (surgery/trauma), never from raw damage alone, so 90+ damage still reads Mangled.
        var steps = new (FixedPoint2 dealThisStep, FixedPoint2 cumulativeTotal, WoundableSeverity expected)[]
        {
            (FixedPoint2.New(9), FixedPoint2.New(9), WoundableSeverity.Healthy),
            (FixedPoint2.New(1), FixedPoint2.New(10), WoundableSeverity.Minor),
            (FixedPoint2.New(9), FixedPoint2.New(19), WoundableSeverity.Minor),
            (FixedPoint2.New(1), FixedPoint2.New(20), WoundableSeverity.Moderate),
            (FixedPoint2.New(19), FixedPoint2.New(39), WoundableSeverity.Moderate),
            (FixedPoint2.New(1), FixedPoint2.New(40), WoundableSeverity.Severe),
            (FixedPoint2.New(19), FixedPoint2.New(59), WoundableSeverity.Severe),
            (FixedPoint2.New(1), FixedPoint2.New(60), WoundableSeverity.Critical),
            (FixedPoint2.New(14), FixedPoint2.New(74), WoundableSeverity.Critical),
            (FixedPoint2.New(1), FixedPoint2.New(75), WoundableSeverity.Mangled),
            (FixedPoint2.New(14), FixedPoint2.New(89), WoundableSeverity.Mangled),
            (FixedPoint2.New(50), FixedPoint2.New(139), WoundableSeverity.Mangled),
        };

        foreach (var (dealThisStep, cumulativeTotal, expected) in steps)
        {
            await server.WaitPost(() =>
            {
                sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, dealThisStep), ignoreResistances: true);
            });

            // Single tick, not several - kept minimal even with BarotraumaComponent removed
            // above, in case any other passive/ambient damage source exists on a bare test map.
            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var states = sWound.GetDamageableStatesOnBody(human);
                Assert.That(states[TargetBodyPart.Chest], Is.EqualTo(expected),
                    $"At {cumulativeTotal} cumulative damage, expected {expected}.");
            });
        }
    }
}

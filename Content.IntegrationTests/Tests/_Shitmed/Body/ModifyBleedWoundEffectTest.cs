using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Body;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// User report: "chems that stop bleeding should stop bleeding - now they do nothing". Root
/// cause: ModifyBleedEntityEffectSystem (the effect backing reagents like Tranexamic Acid)
/// only ever called SharedBloodstreamSystem.TryModifyBleedAmount, which exclusively writes
/// BleedAmountNotFromWounds - but mobs with organ-based wound support bleed entirely through
/// BleedAmountFromWounds, which is independently recomputed every tick straight from each
/// organ's own BleedInflicterComponent state (see SharedBloodstreamSystem.Wounds.cs's
/// UpdateWounds). Any reduction the reagent applied was silently undone within one tick, same
/// bug class already fixed once for topical healing items in HealingSystem.cs's
/// BloodlossModifier branch. Fixed by routing negative (bleed-reducing) amounts through
/// SharedBloodstreamSystem.TryHealWoundBleeding, which reduces every wound-bearing organ's real
/// BleedInflicterComponent state via WoundSystem.TryHealBleedingWounds instead.
///
/// Goes through the real SharedEntityEffectsSystem.ApplyEffect entry point (not a direct call
/// into the fixed method) so this actually exercises the same wiring a reagent's metabolism
/// tick would use.
/// </summary>
[TestFixture]
[TestOf(typeof(ModifyBleedEntityEffectSystem))]
public sealed class ModifyBleedWoundEffectTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ModifyBleedEffectTestMob
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1

- type: entity
  id: ModifyBleedEffectTestArm
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
";

    [Test]
    public async Task StopBleedingEffectActuallyReducesRealWoundBleeding()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sEffects = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedEntityEffectsSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid mob = default;
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            mob = sEntMan.SpawnEntity("ModifyBleedEffectTestMob", coords);
            arm = sEntMan.SpawnEntity("ModifyBleedEffectTestArm", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(mob, BodyComponent.ContainerID);
            container.Insert(arm, organsContainer);
        });

        await pair.RunTicksSync(5);

        // Wound the arm to start real, wound-based bleeding.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(arm, new DamageSpecifier(proto, FixedPoint2.New(30)), ignoreResistances: true, origin: mob);
        });

        await pair.RunTicksSync(5);

        float bleedAmountBefore = default;
        await server.WaitAssertion(() =>
        {
            var bloodstream = sEntMan.GetComponent<BloodstreamComponent>(mob);
            Assert.That(bloodstream.BleedAmount, Is.GreaterThan(0), "Sanity check: the wound should actually be bleeding before applying the effect.");
            bleedAmountBefore = bloodstream.BleedAmount;
        });

        // Apply the same effect a "stop bleeding" reagent (e.g. Tranexamic Acid) would trigger
        // via its own metabolism tick.
        await server.WaitPost(() =>
        {
            sEffects.ApplyEffect(mob, new ModifyBleed { Amount = -1.5f }, 1f);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var bloodstream = sEntMan.GetComponent<BloodstreamComponent>(mob);
            Assert.That(bloodstream.BleedAmount, Is.LessThan(bleedAmountBefore),
                "A ModifyBleed effect with a negative amount should actually reduce the mob's real bleed rate, not just write to a pool that gets overwritten by the next wound recompute tick.");
        });
    }
}

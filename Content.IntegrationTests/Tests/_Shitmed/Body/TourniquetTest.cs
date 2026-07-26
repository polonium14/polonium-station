// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Tourniquet;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Tourniquet;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
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

[TestFixture]
[TestOf(typeof(TourniquetSystem))]
public sealed class TourniquetTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> PiercingDamageType = "Piercing";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TourniquetTestSelf
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: Consciousness
    threshold: 95
    cap: 190
  - type: Targeting

- type: entity
  id: TourniquetTestArm
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
  id: TourniquetTestHand
  components:
  - type: Organ
    category: HandLeft
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 60
    thresholds:
      Healthy: 60
      Minor: 48
      Moderate: 36
      Severe: 24
      Critical: 12
      Mangled: 4
      Severed: 0

- type: entity
  id: TourniquetTestBystander
  components:
  - type: Targeting
  - type: DoAfter

- type: entity
  id: TourniquetTestBodylessTarget

- type: entity
  id: TourniquetTestTorso
  components:
  - type: Organ
    category: Torso
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 200
    thresholds:
      Healthy: 200
      Minor: 160
      Moderate: 120
      Severe: 80
      Critical: 40
      Mangled: 14
      Severed: 0
";

    private async Task<(EntityUid Self, EntityUid Arm, EntityUid Hand, MapCoordinates Coords, IEntityManager EntMan, WoundSystem Wound)> Setup()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid self = default;
        EntityUid arm = default;
        EntityUid hand = default;

        await server.WaitPost(() =>
        {
            self = sEntMan.SpawnEntity("TourniquetTestSelf", coords);
            arm = sEntMan.SpawnEntity("TourniquetTestArm", coords);
            hand = sEntMan.SpawnEntity("TourniquetTestHand", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(self, BodyComponent.ContainerID);
            container.Insert(arm, organsContainer);
            container.Insert(hand, organsContainer);

            sEntMan.GetComponent<TargetingComponent>(self).Target = TargetBodyPart.LeftArm;
        });

        await pair.RunTicksSync(5);

        // Wound both the arm and the hand directly (bypassing the mob-level mirror, which would
        // only hit whichever single organ is currently targeted) so the tourniquet has something
        // to actually block on both organs.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            sDamageable.TryChangeDamage(arm, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: self);
            sDamageable.TryChangeDamage(hand, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: self);
        });

        await pair.RunTicksSync(5);

        return (self, arm, hand, coords, sEntMan, sWound);
    }

    private async Task<EntityUid> ApplyTourniquet(EntityUid self, MapCoordinates coords, IEntityManager sEntMan)
    {
        var server = Pair.Server;
        EntityUid tourniquet = default;

        await server.WaitPost(() =>
        {
            tourniquet = sEntMan.SpawnEntity("Tourniquet", coords);
            var doAfterArgs = new DoAfterArgs(sEntMan, self, TimeSpan.FromSeconds(1), new TourniquetDoAfterEvent("ArmLeft"), self, target: self, used: tourniquet);
            var ev = new TourniquetDoAfterEvent("ArmLeft")
            {
                DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero),
            };
            sEntMan.EventBus.RaiseLocalEvent(self, ev);
        });

        await Pair.RunTicksSync(5);
        return tourniquet;
    }

    private static bool AnyWoundHasModifier(IEntityManager sEntMan, WoundSystem sWound, EntityUid organ, string identifier)
    {
        var woundable = sEntMan.GetComponent<WoundableComponent>(organ);
        foreach (var wound in sWound.GetWoundableWounds(organ, woundable))
        {
            if (sEntMan.TryGetComponent<BleedInflicterComponent>(wound, out var bleeds) && bleeds.BleedingModifiers.ContainsKey(identifier))
                return true;
        }

        return false;
    }

    [Test]
    public async Task TourniquetOnArmAddsBleedAndPainModifiersToArmAndCascadesToHand()
    {
        var (self, arm, hand, coords, sEntMan, sWound) = await Setup();
        var server = Pair.Server;

        var tourniquet = await ApplyTourniquet(self, coords, sEntMan);

        await server.WaitAssertion(() =>
        {
            Assert.That(AnyWoundHasModifier(sEntMan, sWound, arm, "TourniquetPresent"), Is.True,
                "Tourniqueting the arm should add the bleed-block modifier to the arm's own wound(s).");
            Assert.That(AnyWoundHasModifier(sEntMan, sWound, hand, "TourniquetPresent"), Is.True,
                "Tourniqueting the arm should cascade the bleed-block modifier onto the hand below it (LimbTargetMap's cascade children) - a real arm tourniquet cuts off blood to everything downstream.");

            var armNerve = sEntMan.GetComponent<NerveComponent>(arm);
            var handNerve = sEntMan.GetComponent<NerveComponent>(hand);
            Assert.That(armNerve.PainFeelingModifiers.ContainsKey((tourniquet, "Tourniquet")), Is.True,
                "Tourniqueting the arm should numb pain on the arm itself.");
            Assert.That(handNerve.PainFeelingModifiers.ContainsKey((tourniquet, "Tourniquet")), Is.True,
                "The pain-numbing should cascade to the hand too, matching the bleed-block cascade.");

            var tourniquetComp = sEntMan.GetComponent<TourniquetComponent>(tourniquet);
            Assert.That(tourniquetComp.OrganTourniqueted, Is.EqualTo(arm), "The tourniquet should remember the arm as the tourniqueted organ, not the hand.");
        });
    }

    [Test]
    public async Task RemovingTourniquetClearsBleedAndPainModifiersFromBothOrgans()
    {
        var (self, arm, hand, coords, sEntMan, sWound) = await Setup();
        var server = Pair.Server;

        var tourniquet = await ApplyTourniquet(self, coords, sEntMan);

        await server.WaitPost(() =>
        {
            var doAfterArgs = new DoAfterArgs(sEntMan, self, TimeSpan.FromSeconds(1), new RemoveTourniquetDoAfterEvent(), self, target: self, used: tourniquet);
            var ev = new RemoveTourniquetDoAfterEvent
            {
                DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero),
            };
            sEntMan.EventBus.RaiseLocalEvent(self, ev);
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(AnyWoundHasModifier(sEntMan, sWound, arm, "TourniquetPresent"), Is.False,
                "Removing the tourniquet should clear the bleed-block modifier from the arm.");
            Assert.That(AnyWoundHasModifier(sEntMan, sWound, hand, "TourniquetPresent"), Is.False,
                "Removing the tourniquet should also clear the cascaded modifier from the hand.");

            var armNerve = sEntMan.GetComponent<NerveComponent>(arm);
            var handNerve = sEntMan.GetComponent<NerveComponent>(hand);
            Assert.That(armNerve.PainFeelingModifiers.ContainsKey((tourniquet, "Tourniquet")), Is.False,
                "Removing the tourniquet should clear the arm's pain-numbing.");
            Assert.That(handNerve.PainFeelingModifiers.ContainsKey((tourniquet, "Tourniquet")), Is.False,
                "Removing the tourniquet should clear the hand's cascaded pain-numbing too.");

            var tourniquetComp = sEntMan.GetComponent<TourniquetComponent>(tourniquet);
            Assert.That(tourniquetComp.OrganTourniqueted, Is.Null, "The tourniquet should forget which organ it was on once removed.");
        });
    }

    [Test]
    public async Task SwitchingTargetMidApplicationDoesNotRetargetTheTourniquet()
    {
        var (self, arm, hand, coords, sEntMan, sWound) = await Setup();
        var server = Pair.Server;

        EntityUid torso = default;
        await server.WaitPost(() =>
        {
            torso = sEntMan.SpawnEntity("TourniquetTestTorso", coords);
            var container = sEntMan.System<Robust.Shared.Containers.SharedContainerSystem>();
            var organsContainer = container.GetContainer(self, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
        });

        await Pair.RunTicksSync(5);

        // Wound the torso too, so a (buggy) tourniquet-on-torso would have a wound to mark.
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: self);
        });

        await Pair.RunTicksSync(5);

        EntityUid tourniquet = default;
        await server.WaitPost(() =>
        {
            // Same shape as ApplyTourniquet - "ArmLeft" is the category TryTourniquet would have
            // validated (against the real "Tourniquet" item's BlockedCategories: Head/Torso/
            // Groin) at DoAfter-start, while the user's target was still LeftArm.
            tourniquet = sEntMan.SpawnEntity("Tourniquet", coords);
            var doAfterArgs = new DoAfterArgs(sEntMan, self, TimeSpan.FromSeconds(1), new TourniquetDoAfterEvent("ArmLeft"), self, target: self, used: tourniquet);
            var ev = new TourniquetDoAfterEvent("ArmLeft")
            {
                DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero),
            };

            // The exploit: switch the live target to a blocked category (Chest/Torso) right
            // before the DoAfter completes - simulates the user changing their selection during
            // the real DoAfter's delay.
            sEntMan.GetComponent<TargetingComponent>(self).Target = TargetBodyPart.Chest;

            sEntMan.EventBus.RaiseLocalEvent(self, ev);
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var tourniquetComp = sEntMan.GetComponent<TourniquetComponent>(tourniquet);
            Assert.That(tourniquetComp.OrganTourniqueted, Is.EqualTo(arm),
                "The tourniquet should still land on the arm - the category validated at DoAfter-start, not whatever the live target switched to.");
            Assert.That(AnyWoundHasModifier(sEntMan, sWound, arm, "TourniquetPresent"), Is.True,
                "The arm (the actually-validated target) should have the bleed-block modifier.");
            Assert.That(AnyWoundHasModifier(sEntMan, sWound, torso, "TourniquetPresent"), Is.False,
                "The torso should NOT be tourniqueted - it was never validated against BlockedCategories, and blocking it directly would have been rejected at DoAfter-start anyway.");
        });
    }

    [Test]
    public async Task TryTourniquetChecksTheTargetsBodyNotTheUsers()
    {
        var server = Pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sTourniquet = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TourniquetSystem>();
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        var tryTourniquetMethod = typeof(TourniquetSystem).GetMethod("TryTourniquet", BindingFlags.Instance | BindingFlags.NonPublic)!;

        EntityUid user = default;
        EntityUid bodylessTarget = default;
        EntityUid validTarget = default;
        EntityUid tourniquetItem = default;

        await server.WaitPost(() =>
        {
            // The user itself deliberately has no Body/Consciousness - only Targeting, which is
            // all TryTourniquet should need from the user now (e.g. a borg or something applying
            // a tourniquet to a real patient shouldn't be blocked by its own lack of a body).
            user = sEntMan.SpawnEntity("TourniquetTestBystander", coords);
            bodylessTarget = sEntMan.SpawnEntity("TourniquetTestBodylessTarget", coords);
            validTarget = sEntMan.SpawnEntity("TourniquetTestSelf", coords);
            tourniquetItem = sEntMan.SpawnEntity("Tourniquet", coords);

            sEntMan.GetComponent<TargetingComponent>(user).Target = TargetBodyPart.LeftArm;
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var tourniquetComp = sEntMan.GetComponent<TourniquetComponent>(tourniquetItem);

            var bodylessResult = (bool) tryTourniquetMethod.Invoke(sTourniquet, new object[] { bodylessTarget, user, tourniquetItem, tourniquetComp })!;
            Assert.That(bodylessResult, Is.False, "Applying a tourniquet to a body-less target should fail immediately, not silently after a full DoAfter delay.");

            var validResult = (bool) tryTourniquetMethod.Invoke(sTourniquet, new object[] { validTarget, user, tourniquetItem, tourniquetComp })!;
            Assert.That(validResult, Is.True, "Applying to a real target should still succeed even though the user itself has no Body/Consciousness.");
        });
    }

    [Test]
    public async Task NewWoundsOnAnAlreadyTourniquetedLimbStillGetBlocked()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid self = default;
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            self = sEntMan.SpawnEntity("TourniquetTestSelf", coords);
            arm = sEntMan.SpawnEntity("TourniquetTestArm", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(self, BodyComponent.ContainerID);
            container.Insert(arm, organsContainer);

            sEntMan.GetComponent<TargetingComponent>(self).Target = TargetBodyPart.LeftArm;
        });

        await pair.RunTicksSync(5);

        // Apply the tourniquet to a still-unwounded arm - no existing wound for the organ-level
        // TryAddBleedModifier call to have touched.
        await ApplyTourniquet(self, coords, sEntMan);

        // Now wound the arm for the first time, AFTER the tourniquet is already on.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            sDamageable.TryChangeDamage(arm, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: self);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(AnyWoundHasModifier(sEntMan, sWound, arm, "TourniquetPresent"), Is.True,
                "A wound created AFTER the tourniquet was applied should still get the bleed-block modifier, not just the wounds that existed at application time.");
        });
    }
}

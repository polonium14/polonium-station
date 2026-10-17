// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

/// <summary>
/// Code-review finding: OnBleedsTreatmentStep's heal budget (healAmount) was never actually
/// consumed across the per-wound loop - the "full cure" branch zeroed bleeds.Scaling BEFORE
/// subtracting it from healAmount (so that subtraction always subtracted zero), and the
/// "partial reduce" branch never decremented healAmount or broke out either. Net effect: every
/// bleeding wound on a limb got evaluated against the full, un-consumed budget independently -
/// a single application of "Clamp Bleeders" (amount: 2) fully cured/reduced every wound on the
/// limb instead of being capped to a shared budget of 2. Fixed to match the sibling
/// OnTraumaTreatmentStep's OrganDamage case: subtract the actual amount consumed, break once
/// the budget runs out.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedSurgerySystem))]
public sealed class ClampBleedersBudgetTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ClampBleedersBudgetTestOrgan
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

    [Test]
    public async Task ClampBleedersOnlySpendsItsBudgetOnceAcrossMultipleWounds()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var organ = SEntMan.SpawnEntity("ClampBleedersBudgetTestOrgan", coords);
            var container = SEntMan.System<SharedContainerSystem>();
            var woundContainer = container.GetContainer(organ, WoundableComponent.WoundContainerId);
            var wound1 = SEntMan.SpawnEntity(null, coords);
            SEntMan.AddComponent<WoundComponent>(wound1);
            var bleeds1 = SEntMan.AddComponent<BleedInflicterComponent>(wound1);
            bleeds1.Scaling = FixedPoint2.New(1);
            bleeds1.BleedingAmountRaw = FixedPoint2.New(1);
            bleeds1.IsBleeding = true;
            container.Insert(wound1, woundContainer);

            var wound2 = SEntMan.SpawnEntity(null, coords);
            SEntMan.AddComponent<WoundComponent>(wound2);
            var bleeds2 = SEntMan.AddComponent<BleedInflicterComponent>(wound2);
            bleeds2.Scaling = FixedPoint2.New(5);
            bleeds2.BleedingAmountRaw = FixedPoint2.New(1);
            bleeds2.IsBleeding = true;
            container.Insert(wound2, woundContainer);

            // Check the step immediately so passive healing cannot alter the budget result.
            var step = SEntMan.SpawnEntity("SurgeryStepClampBleeders", coords);
            var ev = new SurgeryStepEvent(organ, organ, organ, step, step, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            Assert.That(bleeds1.Scaling, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(bleeds1.IsBleeding, Is.False);
            Assert.That(bleeds2.Scaling, Is.EqualTo(FixedPoint2.New(4)),
                "The second wound receives only the remaining point from the shared budget.");
            Assert.That(bleeds2.IsBleeding, Is.True);
            Assert.That(SEntMan.GetComponent<WoundableComponent>(organ).Bleeds, Is.EqualTo(FixedPoint2.New(4)));
        });
    }

    [TestCase(2, 0)]
    [TestCase(3, 1)]
    public async Task ClampSkipsInactiveWoundsAndUpdatesBleedingImmediately(int scaling, int remaining)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var organ = SEntMan.SpawnEntity("ClampBleedersBudgetTestOrgan", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            var woundContainer = containers.GetContainer(organ, WoundableComponent.WoundContainerId);
            var inactive = SEntMan.SpawnEntity(null, coords);
            SEntMan.AddComponent<WoundComponent>(inactive);
            SEntMan.AddComponent<BleedInflicterComponent>(inactive).Scaling = FixedPoint2.New(20);
            containers.Insert(inactive, woundContainer);
            var active = SEntMan.SpawnEntity(null, coords);
            SEntMan.AddComponent<WoundComponent>(active);
            var bleed = SEntMan.AddComponent<BleedInflicterComponent>(active);
            bleed.Scaling = FixedPoint2.New(scaling);
            bleed.ScalingLimit = FixedPoint2.New(10);
            bleed.BleedingAmountRaw = FixedPoint2.New(4);
            bleed.IsBleeding = true;
            containers.Insert(active, woundContainer);
            var wounds = SEntMan.System<Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems.WoundSystem>();
            wounds.RecomputeWoundableBleeds(organ);
            var step = SEntMan.SpawnEntity("SurgeryStepClampBleeders", coords);
            var ev = new SurgeryStepEvent(organ, organ, organ, step, step, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            Assert.That(bleed.Scaling, Is.EqualTo(FixedPoint2.New(remaining)));
            Assert.That(bleed.IsBleeding, Is.EqualTo(remaining > 0));
            Assert.That(SEntMan.GetComponent<WoundableComponent>(organ).Bleeds, Is.EqualTo(FixedPoint2.New(4 * remaining)));
            if (remaining == 0)
            {
                Assert.That(bleed.BleedingAmountRaw, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(bleed.ScalingLimit, Is.EqualTo(BleedInflicterComponent.DefaultScalingLimit));
            }
        });
    }

}

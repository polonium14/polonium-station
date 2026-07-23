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
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid organ = default;
        EntityUid wound1 = default;
        EntityUid wound2 = default;
        EntityUid stepEnt = default;

        await server.WaitPost(() =>
        {
            organ = sEntMan.SpawnEntity("ClampBleedersBudgetTestOrgan", coords);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var woundable = sEntMan.GetComponent<WoundableComponent>(organ);
            var container = sEntMan.System<SharedContainerSystem>();

            // Two synthetic wounds inserted directly into the organ's own Wounds container -
            // GetWoundableWounds requires WoundComponent to recognize a container entry as a
            // wound at all (defaults are fine, only its presence matters here); OnBleedsTreatmentStep
            // itself only cares about BleedInflicterComponent, not the rest of Wound's real
            // prototype/severity machinery.
            wound1 = sEntMan.SpawnEntity(null, coords);
            sEntMan.AddComponent<WoundComponent>(wound1);
            var bleeds1 = sEntMan.AddComponent<BleedInflicterComponent>(wound1);
            bleeds1.Scaling = FixedPoint2.New(1);
            bleeds1.IsBleeding = true;

            wound2 = sEntMan.SpawnEntity(null, coords);
            sEntMan.AddComponent<WoundComponent>(wound2);
            var bleeds2 = sEntMan.AddComponent<BleedInflicterComponent>(wound2);
            bleeds2.Scaling = FixedPoint2.New(5);
            bleeds2.IsBleeding = true;

            container.Insert(wound1, woundable.Wounds!);
            container.Insert(wound2, woundable.Wounds!);

            stepEnt = sEntMan.SpawnEntity("SurgeryStepClampBleeders", coords);
        });

        await pair.RunTicksSync(5);

        // amount: 2 (SurgeryStepClampBleeders's own prototype value). Budget spend order:
        // wound1 (Scaling 1) fully cured first, consuming 1 of the 2-point budget: 1 left.
        // wound2 (Scaling 5) only gets that remaining 1 point: 5 - 1 = 4, still bleeding.
        await server.WaitPost(() =>
        {
            var ev = new SurgeryStepEvent(organ, organ, organ, stepEnt, stepEnt, stepEnt);
            sEntMan.EventBus.RaiseLocalEvent(stepEnt, ref ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var bleeds1 = sEntMan.GetComponent<BleedInflicterComponent>(wound1);
            var bleeds2 = sEntMan.GetComponent<BleedInflicterComponent>(wound2);

            Assert.That(bleeds1.Scaling, Is.EqualTo(FixedPoint2.Zero), "The first wound (Scaling 1, fully within budget) should be fully cured.");
            Assert.That(bleeds1.IsBleeding, Is.False, "The first wound should have stopped bleeding.");

            // This is the core regression check: the old bug re-applied the full nominal budget
            // to every wound independently (this would read 3 here, 5 - 2), instead of only the
            // remaining share of one shared budget (5 - 1 = 4).
            Assert.That(bleeds2.Scaling, Is.EqualTo(FixedPoint2.New(4)),
                "The second wound should only have absorbed the REMAINING 1 point of the shared 2-point budget (5 - 1 = 4), not the full nominal amount again (which would wrongly read 3).");
        });
    }
}

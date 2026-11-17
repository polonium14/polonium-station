// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Administration.Systems;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// An open surgical incision bleeds for as long as it stays open, wherever the patient lies:
/// strongly until its bleeders are clamped, weakly afterwards. The bleeding does not clot
/// and stops when the incision is closed (see HealthAnalyzerUnfinishedSurgeryTest for the
/// marker-detection half of this feature).
/// </summary>
[TestFixture]
[TestOf(typeof(UnfinishedSurgeryPenaltySystem))]
public sealed class UnfinishedSurgeryBleedingPenaltyTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: UnfinishedSurgeryPenaltyMob
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
  id: UnfinishedSurgeryPenaltyTorso
  components:
  - type: Organ
    category: Torso
";

    // The default surgery.open_incision_bleed and surgery.clamped_incision_bleed for the torso.
    private const float OpenBleed = 2.5f;
    private const float ClampedBleed = 0.3f;

    private async Task RunSeconds(float seconds)
    {
        await Pair.RunTicksSync((int) (seconds / SGameTiming.TickPeriod.TotalSeconds));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OpenIncisionBleedsUntilClosed(bool rejuvenate)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid mob = default;
        EntityUid torso = default;

        await Server.WaitPost(() =>
        {
            mob = SEntMan.SpawnEntity("UnfinishedSurgeryPenaltyMob", coords);
            torso = SEntMan.SpawnEntity("UnfinishedSurgeryPenaltyTorso", coords);

            var container = SEntMan.System<SharedContainerSystem>();
            container.Insert(torso, container.GetContainer(mob, BodyComponent.ContainerID));

            // The patient lies on the floor: no bed or operating table is involved.
            SEntMan.AddComponent<IncisionOpenComponent>(torso);
        });

        // Long enough for several bleed updates, so clotting would show if it happened.
        await RunSeconds(15);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BloodstreamComponent>(mob).BleedAmount, Is.EqualTo(OpenBleed).Within(0.01f),
                "An unclamped incision bleeds at the full rate and does not clot.");
            SEntMan.AddComponent<BleedersClampedComponent>(torso);
        });

        await RunSeconds(6);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BloodstreamComponent>(mob).BleedAmount, Is.EqualTo(ClampedBleed).Within(0.01f),
                "Clamping the bleeders leaves only a slow seep.");

            if (!rejuvenate)
            {
                // Closing the incision removes its markers.
                SEntMan.RemoveComponent<IncisionOpenComponent>(torso);
                SEntMan.RemoveComponent<BleedersClampedComponent>(torso);
                return;
            }

            // Rejuvenation must clear every unfinished step and remove the tracking immediately.
            SEntMan.AddComponent<SkinRetractedComponent>(torso);
            SEntMan.AddComponent<InternalBleedersClampedComponent>(torso);
            SEntMan.AddComponent<BonesOpenComponent>(torso);
            SEntMan.AddComponent<BonesSawedComponent>(torso);
            SEntMan.AddComponent<BodyPartSawedComponent>(torso);
            SEntMan.AddComponent<SeveredSkinRemovedComponent>(torso);
            SEntMan.AddComponent<BoneLeftoversRemovedComponent>(torso);
            SEntMan.AddComponent<BodyPartReattachedComponent>(torso);
            SEntMan.AddComponent<OrganReattachedComponent>(torso);
            SEntMan.AddComponent<LobotomizedComponent>(torso);
            SEntMan.AddComponent<PartsRemovedComponent>(torso);

            SEntMan.System<RejuvenateSystem>().PerformRejuvenate(mob);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<SharedSurgerySystem>().HasUnfinishedSurgerySteps(torso), Is.False);
                Assert.That(SEntMan.HasComponent<LobotomizedComponent>(torso), Is.False);
                Assert.That(SEntMan.HasComponent<PartsRemovedComponent>(torso), Is.False);
                Assert.That(SEntMan.HasComponent<UnfinishedSurgeryPenaltyComponent>(mob), Is.False);
                Assert.That(SEntMan.GetComponent<BloodstreamComponent>(mob).BleedAmount, Is.Zero);
            });
        });

        await RunSeconds(6);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<UnfinishedSurgeryPenaltyComponent>(mob), Is.False,
                    "A closed incision leaves no tracking behind.");
                Assert.That(SEntMan.GetComponent<BloodstreamComponent>(mob).BleedAmount, Is.Zero,
                    "A closed incision stops bleeding.");
            });
        });
    }
}

// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.Body;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class SurgeryStepCompletionPersistsTest : GameTest
{
    [Test]
    public async Task IncisionStaysCompleteAfterSurgeryPainExpires()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSystems = server.ResolveDependency<IEntitySystemManager>();
        var sSurgery = sSystems.GetEntitySystem<SurgerySystem>();
        var sConsciousness = sSystems.GetEntitySystem<ConsciousnessSystem>();
        var sPain = sSystems.GetEntitySystem<PainSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid head = default;
        EntityUid scalpel = default;
        EntityUid retractor = default;
        EntityUid hemostat = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("MobHuman", coords);
            scalpel = sEntMan.SpawnEntity("Scalpel", coords);
            retractor = sEntMan.SpawnEntity("Retractor", coords);
            hemostat = sEntMan.SpawnEntity("Hemostat", coords);
        });

        await pair.RunTicksSync(5);

        EntityUid openIncision = default;
        EntityUid scalpelStep = default;

        await server.WaitAssertion(() =>
        {
            var body = sEntMan.GetComponent<BodyComponent>(victim);
            Assert.That(body.Organs, Is.Not.Null, "Setup: MobHuman should have an organs container.");

            foreach (var organ in body.Organs!.ContainedEntities)
            {
                if (sEntMan.GetComponent<OrganComponent>(organ).Category?.Id == "Head")
                {
                    head = organ;
                    break;
                }
            }

            Assert.That(head, Is.Not.EqualTo(default(EntityUid)), "Setup: MobHuman should have a Head organ.");

            openIncision = sSurgery.GetSingleton("SurgeryOpenIncision")!.Value;
            scalpelStep = sSurgery.GetSingleton("SurgeryStepOpenIncisionScalpel")!.Value;

            var next = sSurgery.GetNextStep(victim, head, openIncision, victim);
            Assert.That(next, Is.Not.Null, "Sanity: a fresh head should have surgery steps left.");
            Assert.That(next!.Value.Step,
                Is.EqualTo(0),
                "Sanity: the first incomplete step of a fresh head should be the scalpel cut.");
        });

        await server.WaitPost(() =>
        {
            var ev = new SurgeryStepEvent(victim, victim, head, scalpel, openIncision, scalpelStep);
            sEntMan.EventBus.RaiseLocalEvent(scalpelStep, ref ev);
        });

        await pair.RunTicksSync(5);

        EntityUid nerveSystemOwner = default;

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.HasComponent<IncisionOpenComponent>(head),
                Is.True,
                "The scalpel cut should have left an IncisionOpen marker on the head.");

            var next = sSurgery.GetNextStep(victim, head, openIncision, victim);
            Assert.That(next, Is.Not.Null);
            Assert.That(next!.Value.Step,
                Is.EqualTo(1),
                "After the cut, the next incomplete step should be 'clamp the bleeders' - the scalpel step must read as complete.");

            Assert.That(sConsciousness.TryGetNerveSystem(victim, out var nerveSys),
                Is.True,
                "Setup: a real MobHuman should have a nerve system.");
            nerveSystemOwner = nerveSys!.Value.Owner;
            Assert.That(sPain.TryGetPainModifier(nerveSystemOwner, head, "SurgeryPain", out _),
                Is.True,
                "Setup: the scalpel cut should have inflicted real SurgeryPain on the head's nerve.");
        });

        await server.WaitPost(() =>
        {
            sPain.TryRemovePainModifier(nerveSystemOwner, head, "SurgeryPain");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var next = sSurgery.GetNextStep(victim, head, openIncision, victim);
            Assert.That(next, Is.Not.Null);
            Assert.That(next!.Value.Step,
                Is.EqualTo(1),
                "The scalpel step must STAY complete after the surgery pain is gone - with the " +
                "old pain-presence completion check this regressed to step 0, reproducing the " +
                "live 'complete the previous step first' bug.");
        });

        await server.WaitPost(() =>
        {
            var clampStep = sSurgery.GetSingleton("SurgeryStepClampIncision")!.Value;
            var clamp = new SurgeryStepEvent(victim, victim, head, hemostat, openIncision, clampStep);
            sEntMan.EventBus.RaiseLocalEvent(clampStep, ref clamp);

            var retractStep = sSurgery.GetSingleton("SurgeryStepRetractSkin")!.Value;
            var ev = new SurgeryStepEvent(victim, victim, head, retractor, openIncision, retractStep);
            sEntMan.EventBus.RaiseLocalEvent(retractStep, ref ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sSurgery.GetNextStep(victim, head, openIncision, victim),
                Is.Null,
                "All OpenIncision steps are done - the surgery should have no next step.");

            var stopBloodOutput = sSurgery.GetSingleton("SurgeryStopBloodOutput")!.Value;
            var next = sSurgery.GetNextStep(victim, head, stopBloodOutput, victim);
            Assert.That(next,
                Is.Not.Null,
                "SurgeryStopBloodOutput should have steps left - only its requirement is satisfied.");
            Assert.That(next!.Value.Surgery.Owner,
                Is.EqualTo(stopBloodOutput),
                "The requirement (SurgeryOpenIncision) is fully complete, so the next step must come from SurgeryStopBloodOutput itself, not the requirement.");
            Assert.That(next.Value.Step,
                Is.EqualTo(0),
                "The next step should be SurgeryStopBloodOutput's own first step (clamp bleeders).");

            // Stitching clamped wounds releases their clamp, not the incision's.
            sEntMan.AddComponent<BleedersClampedComponent>(head);
            var stitchStep = sSurgery.GetSingleton("SurgeryStepCloseBloodOutputs")!.Value;
            var stitches = sEntMan.SpawnEntity(null, coords);
            sEntMan.AddComponent<StitchesComponent>(stitches);
            var stitch = new SurgeryStepEvent(victim, victim, head, stitches, stopBloodOutput, stitchStep);
            sEntMan.EventBus.RaiseLocalEvent(stitchStep, ref stitch);
            Assert.That(sEntMan.HasComponent<BleedersClampedComponent>(head), Is.False);
            Assert.That(sEntMan.HasComponent<IncisionClampedComponent>(head), Is.True);
            Assert.That(sSurgery.GetNextStep(victim, head, openIncision, victim),
                Is.Null,
                "The incision stays clamped and open for the next surgery.");
        });
    }
}

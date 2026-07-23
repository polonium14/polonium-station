// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Effects.Step;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class SurgeryScreamOnPainTest : GameTest
{
    /// <summary>
    /// Records every EmoteEvent fired at an entity carrying TestListenerComponent - used here to
    /// observe ChatSystem.TryEmoteWithChat's real side effect (the "Scream" EmoteEvent) without
    /// having to parse chat log output.
    /// </summary>
    public sealed class ScreamListenerSystem : TestListenerSystem<EmoteEvent>;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SurgeryScreamTestVictim
  components:
  - type: Body
  - type: Vocal
  - type: Emoting

- type: entity
  id: SurgeryScreamTestTool

# Minimal status-effect entity carrying only ForcedSleepingStatusEffectComponent - exercises the
# exact same Status.HasEffectComp<ForcedSleepingStatusEffectComponent> code path OnStepScreamComplete
# checks, without dragging in the production StatusEffectForcedSleeping prototype's Stunned/
# Knockdown baggage (which requires a full mob setup this test doesn't need).
- type: entity
  parent: StatusEffectBase
  id: SurgeryScreamTestAnesthesia
  components:
  - type: ForcedSleepingStatusEffect
";

    [Test]
    public async Task NonAnesthetizedPatientScreamsOnPainInflictingStep()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var sListener = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<ScreamListenerSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid tool = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("SurgeryScreamTestVictim", coords);
            tool = sEntMan.SpawnEntity("SurgeryScreamTestTool", coords);
            sEntMan.EnsureComponent<TestListenerComponent>(victim);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var stepEnt = sSurgery.GetSingleton("SurgeryStepSawBones");
            Assert.That(stepEnt, Is.Not.Null, "SurgeryStepSawBones should resolve to a real singleton entity.");
            Assert.That(sEntMan.HasComponent<SurgeryStepEmoteEffectComponent>(stepEnt!.Value),
                Is.True, "Setup: SurgeryStepSawBones should carry SurgeryStepEmoteEffectComponent (the fix under test).");

            var ev = new SurgeryStepEvent(victim, victim, victim, tool, default, stepEnt!.Value);
            sEntMan.EventBus.RaiseLocalEvent(stepEnt.Value, ref ev);
        });

        await pair.RunTicksSync(5);

        Assert.That(sListener.Count(victim, e => e.Emote.ID == "Scream"), Is.EqualTo(1),
            "A non-anesthetized patient should scream (fire the \"Scream\" EmoteEvent) when a " +
            "real pain-inflicting surgery step (SurgeryStepSawBones) completes on it - this is " +
            "the exact mechanic the user reported as broken vs `master`.");
    }

    [Test]
    public async Task AnesthetizedPatientDoesNotScreamOnPainInflictingStep()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var sListener = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<ScreamListenerSystem>();
        var sStatusEffects = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<StatusEffectsSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid tool = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("SurgeryScreamTestVictim", coords);
            tool = sEntMan.SpawnEntity("SurgeryScreamTestTool", coords);
            sEntMan.EnsureComponent<TestListenerComponent>(victim);

            var added = sStatusEffects.TryAddStatusEffectDuration(victim, "SurgeryScreamTestAnesthesia", TimeSpan.FromSeconds(60));
            Assert.That(added, Is.True, "Setup: the anesthesia status effect should apply cleanly to the test victim.");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sStatusEffects.HasEffectComp<ForcedSleepingStatusEffectComponent>(victim),
                Is.True,
                "Setup: victim should be recognized as anesthetized (ForcedSleepingStatusEffectComponent present) before exercising the step.");

            var stepEnt = sSurgery.GetSingleton("SurgeryStepSawBones");
            Assert.That(stepEnt, Is.Not.Null, "SurgeryStepSawBones should resolve to a real singleton entity.");

            var ev = new SurgeryStepEvent(victim, victim, victim, tool, default, stepEnt!.Value);
            sEntMan.EventBus.RaiseLocalEvent(stepEnt.Value, ref ev);
        });

        await pair.RunTicksSync(5);

        Assert.That(sListener.Count(victim, e => e.Emote.ID == "Scream"), Is.EqualTo(0),
            "An anesthetized patient (ForcedSleepingStatusEffectComponent present) should NOT " +
            "scream when the same pain-inflicting step completes - OnStepScreamComplete's " +
            "anesthesia gate should suppress it.");
    }
}

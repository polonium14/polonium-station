using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared._Polonium.Tutorial.Watchers;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Tutorial;

/// <summary>
/// The shape of a flow, checked without running it: a trainee has to be able to reach the end, and
/// nothing in the content may point at a step that is not there.
/// </summary>
public sealed class TutorialFlowStructureTest : GameTest
{
    [Test]
    public async Task EveryStepInTheFlowResolves()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            var flow = TutorialContent.Flow(proto);
            Assert.That(flow.Steps, Is.Not.Empty, "the basic flow has no steps at all");

            var missing = flow.Steps.Where(id => !proto.HasIndex(id)).ToList();
            Assert.That(missing, Is.Empty, $"flow names steps that do not exist: {string.Join(", ", missing)}");
        });
    }

    [Test]
    public async Task NoStepRunsTwiceInAFlow()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            foreach (var flow in proto.EnumeratePrototypes<TutorialFlowPrototype>())
            {
                var repeated = flow.Steps
                    .GroupBy(id => id.Id)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToList();

                Assert.That(repeated, Is.Empty,
                    $"flow {flow.ID} lists the same step twice: {string.Join(", ", repeated)}");
            }
        });
    }

    /// <summary>
    /// A step nobody walks through is content that cannot be seen, and usually means a rename that
    /// only got as far as the step file.
    /// </summary>
    [Test]
    public async Task EveryStepBelongsToAFlow()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            var used = proto.EnumeratePrototypes<TutorialFlowPrototype>()
                .SelectMany(flow => flow.Steps)
                .Select(id => id.Id)
                .ToHashSet();

            var orphans = proto.EnumeratePrototypes<TutorialStepPrototype>()
                .Select(step => step.ID)
                .Where(id => !used.Contains(id))
                .OrderBy(id => id)
                .ToList();

            Assert.That(orphans, Is.Empty, $"steps no flow ever reaches: {string.Join(", ", orphans)}");
        });
    }

    /// <summary>
    /// Every step has to end somehow. It either has a completion the trainee can meet or it times
    /// out on its own - a blocking step with no completion is a dead end, and so is one that also
    /// turns the stuck timer off.
    /// </summary>
    [Test]
    public async Task EveryStepCanBeLeft()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            var stuck = proto.EnumeratePrototypes<TutorialStepPrototype>()
                .Where(step => step.Completion == null && !step.Finale)
                .Where(step => step.Blocking || step.StuckSkipSeconds == 0f)
                .Select(step => step.ID)
                .ToList();

            Assert.That(stuck, Is.Empty,
                $"steps with no completion that also never time out: {string.Join(", ", stuck)}");
        });
    }

    [Test]
    public async Task EveryEjectLandsOnAStepOfTheSameFlow()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            var flow = TutorialContent.Flow(proto);
            var inFlow = flow.Steps.Select(id => id.Id).ToHashSet();
            var strays = new List<string>();

            foreach (var step in TutorialContent.Steps(proto, flow))
            {
                foreach (var node in TutorialContent.Nodes(proto, step))
                {
                    if (node is EjectTraineeAction eject && !inFlow.Contains(eject.Step.Id))
                        strays.Add($"{step.ID} -> {eject.Step.Id}");
                }
            }

            Assert.That(strays, Is.Empty, $"ejects that jump outside the flow: {string.Join(", ", strays)}");
        });
    }

    /// <summary>A watcher that says nothing and does nothing only costs the trainee a poll.</summary>
    [Test]
    public async Task EveryWatcherReacts()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            var idle = new List<string>();

            foreach (var step in proto.EnumeratePrototypes<TutorialStepPrototype>())
            {
                foreach (var watcher in step.Watchers)
                {
                    if (DoesNothing(watcher))
                        idle.Add($"{step.ID}/{watcher.GetType().Name}");
                }
            }

            foreach (var set in proto.EnumeratePrototypes<TutorialWatcherSetPrototype>())
            {
                foreach (var watcher in set.Watchers)
                {
                    if (DoesNothing(watcher))
                        idle.Add($"{set.ID}/{watcher.GetType().Name}");
                }
            }

            Assert.That(idle, Is.Empty, $"watchers with neither a line nor an action: {string.Join(", ", idle)}");
        });
    }

    [Test]
    public async Task EveryWatcherSetIsArmedSomewhere()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            var used = proto.EnumeratePrototypes<TutorialStepPrototype>()
                .SelectMany(step => step.WatcherSets)
                .Select(id => id.Id)
                .ToHashSet();

            var unused = proto.EnumeratePrototypes<TutorialWatcherSetPrototype>()
                .Select(set => set.ID)
                .Where(id => !used.Contains(id))
                .OrderBy(id => id)
                .ToList();

            Assert.That(unused, Is.Empty, $"watcher sets no step arms: {string.Join(", ", unused)}");
        });
    }

    /// <summary>
    /// The step a trainee is held on until the mentor stops talking has to have something to say,
    /// otherwise that freeze only lifts on the sixty second safety net.
    /// </summary>
    [Test]
    public async Task StepsFrozenForSpeechActuallySpeak()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            var silent = proto.EnumeratePrototypes<TutorialStepPrototype>()
                .Where(step => step.FreezeWhileSpeaking && step.Speak.Count == 0)
                .Select(step => step.ID)
                .ToList();

            Assert.That(silent, Is.Empty,
                $"steps that freeze the trainee for a briefing they never give: {string.Join(", ", silent)}");
        });
    }

    /// <summary>Holding the lines until the trainee reaches an anchor means having lines to hold.</summary>
    [Test]
    public async Task SpeechHeldForAnAnchorHasLines()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            var pointless = proto.EnumeratePrototypes<TutorialStepPrototype>()
                .Where(step => step.SpeakAtAnchor != null && step.Speak.Count == 0)
                .Select(step => step.ID)
                .ToList();

            Assert.That(pointless, Is.Empty,
                $"steps waiting at an anchor with nothing to say there: {string.Join(", ", pointless)}");
        });
    }

    private static bool DoesNothing(TutorialWatcher watcher)
    {
        // a slip teleport puts the trainee back on their feet by itself
        if (watcher is SlipTeleportWatcher)
            return false;

        return watcher.Quip.Count == 0 && watcher.Actions.Count == 0;
    }
}

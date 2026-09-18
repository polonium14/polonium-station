#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.GameTicking;
using Content.Server._Polonium.Tutorial;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared._Polonium.Tutorial.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Tutorial;

/// <summary>
/// Walks a trainee through the whole basic flow on the real map, one forced step at a time. It does
/// not play the lessons - it proves every step can be entered and left: each set of actions runs
/// against the anchors that are actually there, and the harness fails the run on any error logged
/// along the way.
/// </summary>
public sealed class TutorialFlowRunTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private const string TutorialMap = "Tutorial";
    private const string Trainee = "MobHuman";

    // long enough for the tracker to poll once, so a jump queued by an eject lands before the next push
    private const int TicksPerStep = 10;

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.TutorialMode), "Tutorial")]
    public async Task WholeFlowRunsToTheEnd()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var entMan = Server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();
        var tutorial = entMan.System<TutorialSystem>();

        var flow = TutorialContent.Flow(proto);
        EntityUid trainee = default;

        // the mode is replicated and has to be in place before the client connects, so it comes from
        // the attribute. grid fill is server only and only saves time, the pair is thrown away after
        await Server.WaitPost(() => Server.CfgMan.SetCVar(CCVars.GridFill, false));

        await Server.WaitPost(() =>
        {
            var opts = DeserializationOptions.Default with { InitializeMaps = true };
            ticker.LoadGameMap(proto.Index<GameMapPrototype>(TutorialMap), out var mapId, opts);

            trainee = entMan.SpawnEntity(Trainee, StartingPoint(entMan, proto, mapId));

            entMan.System<SharedMindSystem>().WipeMind(ServerSession!.ContentData()?.Mind);
            Server.PlayerMan.SetAttachedEntity(ServerSession, trainee);
        });

        await Server.WaitRunTicks(5);

        await Server.WaitAssertion(() =>
        {
            tutorial.ForceStartFlow(trainee, flow.ID);

            Assert.That(entMan.TryGetComponent(trainee, out TutorialSessionComponent? session), Is.True,
                "starting the flow did not give the trainee a session");
            Assert.That(session!.CurrentStepIndex, Is.EqualTo(0), "the flow did not start on its first step");
        });

        var visited = new List<int>();

        for (var push = 0; push < flow.Steps.Count * 2; push++)
        {
            int? index = null;
            await Server.WaitPost(() =>
            {
                if (entMan.TryGetComponent(trainee, out TutorialSessionComponent? session))
                    index = session.CurrentStepIndex;
            });

            if (index is not { } current)
                break;

            if (visited.Count > 0)
            {
                Assert.That(current, Is.GreaterThanOrEqualTo(visited[^1]),
                    $"the flow went back from {flow.Steps[visited[^1]]} to {flow.Steps[current]}");
            }

            if (visited.Count == 0 || visited[^1] != current)
                visited.Add(current);

            await Server.WaitPost(() => tutorial.ForceAdvance(trainee));
            await Server.WaitRunTicks(TicksPerStep);
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(visited, Is.Not.Empty);
            Assert.That(flow.Steps[visited[^1]].Id, Is.EqualTo(flow.Steps[^1].Id),
                $"the run stopped on {flow.Steps[visited[^1]]} instead of reaching the finale");
            Assert.That(entMan.HasComponent<TutorialSessionComponent>(trainee), Is.False,
                "leaving the finale did not end the flow");
        });
    }

    /// <summary>Where the first step expects the trainee: its navigation anchor, else any anchor at all.</summary>
    private static EntityCoordinates StartingPoint(IEntityManager entMan, IPrototypeManager proto, MapId mapId)
    {
        var wanted = TutorialContent.Steps(proto, TutorialContent.Flow(proto))
            .Select(step => step.NavigationAnchor)
            .FirstOrDefault(id => id != null);

        EntityCoordinates? fallback = null;
        var query = entMan.EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var anchor, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            if (anchor.AnchorId == wanted)
                return xform.Coordinates;

            fallback ??= xform.Coordinates;
        }

        Assert.That(fallback, Is.Not.Null, "the tutorial map has no anchors to start the trainee on");
        return fallback!.Value;
    }
}

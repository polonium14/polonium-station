using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Tutorial;

/// <summary>
/// Anchors are the only thing tying a step to the station: every id a step names has to be on the
/// map, or be put there by an earlier action. An id that matches nothing turns its step into a wait
/// for something that can never happen, and the trainee only finds out when the timer runs out.
/// </summary>
public sealed class TutorialAnchorTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task EveryAnchorTheFlowNamesIsOnTheMap()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var entMan = Server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();

        var onMap = new HashSet<string>();

        await Server.WaitPost(() =>
        {
            var opts = DeserializationOptions.Default with { InitializeMaps = true };
            ticker.LoadGameMap(proto.Index<GameMapPrototype>(TutorialMap), out var mapId, opts);

            var query = entMan.EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
            while (query.MoveNext(out _, out var anchor, out var xform))
            {
                if (xform.MapID == mapId)
                    onMap.Add(anchor.AnchorId);
            }
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(onMap, Is.Not.Empty, "the tutorial map carries no anchors at all");

            var flow = TutorialContent.Flow(proto);
            var provided = new HashSet<string>(onMap);
            var wanted = new Dictionary<string, List<string>>();

            foreach (var step in TutorialContent.Steps(proto, flow))
            {
                foreach (var node in TutorialContent.Nodes(proto, step))
                {
                    // a spawn brings its own anchor into being, it does not expect to find one
                    foreach (var id in TutorialContent.AssignedAnchors(node))
                        provided.Add(id);

                    foreach (var id in TutorialContent.Anchors(node))
                    {
                        if (!wanted.TryGetValue(id, out var steps))
                            wanted[id] = steps = new List<string>();

                        if (!steps.Contains(step.ID))
                            steps.Add(step.ID);
                    }
                }
            }

            var missing = wanted.Keys
                .Where(id => !provided.Contains(id))
                .OrderBy(id => id)
                .Select(id => $"{id} (wanted by {string.Join(", ", wanted[id])})")
                .ToList();

            Assert.That(missing, Is.Empty,
                $"steps name anchors that are neither on the map nor spawned:{Environment.NewLine}"
                + string.Join(Environment.NewLine, missing));
        });
    }

    /// <summary>
    /// The other way round. An anchor nothing asks for is usually a rename that only landed on one
    /// side, and it reads on the map as if some step still used it. Holopads are the exception.
    /// </summary>
    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task EveryAnchorOnTheMapIsUsed()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var entMan = Server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();

        var onMap = new HashSet<string>();

        await Server.WaitPost(() =>
        {
            var opts = DeserializationOptions.Default with { InitializeMaps = true };
            ticker.LoadGameMap(proto.Index<GameMapPrototype>(TutorialMap), out var mapId, opts);

            var query = entMan.EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var anchor, out var xform))
            {
                // the mentor finds holopads by their projector, the id on one is only a label for mappers
                if (xform.MapID == mapId && !entMan.HasComponent<TutorialHoloPointComponent>(uid))
                    onMap.Add(anchor.AnchorId);
            }
        });

        await Server.WaitAssertion(() =>
        {
            var wanted = new HashSet<string>();

            foreach (var flow in proto.EnumeratePrototypes<TutorialFlowPrototype>())
            {
                foreach (var step in TutorialContent.Steps(proto, flow))
                {
                    foreach (var node in TutorialContent.Nodes(proto, step))
                        wanted.UnionWith(TutorialContent.Anchors(node));
                }
            }

            var unused = onMap.Where(id => !wanted.Contains(id)).OrderBy(id => id).ToList();

            Assert.That(unused, Is.Empty,
                $"anchors on the map no step ever names:{Environment.NewLine}"
                + string.Join(Environment.NewLine, unused));
        });
    }

    private const string TutorialMap = "Tutorial";
}

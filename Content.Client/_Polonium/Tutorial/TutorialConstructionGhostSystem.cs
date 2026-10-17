using System.Numerics;
using Content.Client.Construction;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Watchers;
using Robust.Client.Player;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Tutorial;

/// <summary>
/// Construction ghosts never exist on the server, so the current step cannot see them by itself.
/// This watches the client's ghosts against the step's markers and tells the server when that changes.
/// </summary>
public sealed partial class TutorialConstructionGhostSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<ConstructionGhostOnAnchorCondition> _wanted = new();
    private readonly Dictionary<string, (bool OnTile, bool Stray)> _last = new();
    private readonly HashSet<string> _seen = new();

    public override void FrameUpdate(float frameTime)
    {
        if (_player.LocalEntity is not { } player
            || !TryComp<TutorialSessionComponent>(player, out var session)
            || session.CurrentStep is not { } stepId
            || !_proto.TryIndex(stepId, out var step))
        {
            Reset();
            return;
        }

        _wanted.Clear();
        Collect(step.Completion);
        foreach (var watcher in step.Watchers)
        {
            if (watcher is ConditionWatcher cond)
                Collect(cond.Condition);
        }

        if (_wanted.Count == 0)
        {
            Reset();
            return;
        }

        if (Transform(player).GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        _seen.Clear();
        foreach (var cond in _wanted)
        {
            if (!_seen.Add(cond.AnchorId))
                continue;

            var (onTile, stray) = Measure(grid, gridComp, cond);

            if (_last.TryGetValue(cond.AnchorId, out var prev) && prev.OnTile == onTile && prev.Stray == stray)
                continue;

            _last[cond.AnchorId] = (onTile, stray);
            RaiseNetworkEvent(new TutorialConstructionGhostStateEvent(cond.AnchorId, onTile, stray));
        }
    }

    private (bool OnTile, bool Stray) Measure(EntityUid grid, MapGridComponent gridComp, ConstructionGhostOnAnchorCondition cond)
    {
        if (FindAnchor(grid, cond.AnchorId) is not { } anchor)
            return (false, false);

        var anchorTile = _map.TileIndicesFor(grid, gridComp, Transform(anchor).Coordinates);
        var anchorPos = _transform.GetWorldPosition(anchor);
        var recipe = cond.Recipe is { } recipeId ? recipeId.Id : null;
        var onTile = false;
        var stray = false;

        var query = EntityQueryEnumerator<ConstructionGhostComponent, TransformComponent>();
        while (query.MoveNext(out _, out var ghost, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            if (recipe != null && ghost.Prototype?.ID != recipe)
                continue;

            var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
            if (tile == anchorTile)
            {
                onTile = true;
                continue;
            }

            if (Vector2.Distance(_transform.GetWorldPosition(xform), anchorPos) <= cond.StrayRange)
                stray = true;
        }

        return (onTile, stray);
    }

    private void Collect(TutorialCondition? cond)
    {
        switch (cond)
        {
            case ConstructionGhostOnAnchorCondition ghost:
                _wanted.Add(ghost);
                break;
            case AnyCondition any:
                foreach (var inner in any.Conditions)
                    Collect(inner);
                break;
            case AllCondition all:
                foreach (var inner in all.Conditions)
                    Collect(inner);
                break;
        }
    }

    private EntityUid? FindAnchor(EntityUid grid, string anchorId)
    {
        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var xform))
        {
            if (anchor.AnchorId == anchorId && xform.GridUid == grid)
                return uid;
        }

        return null;
    }

    private void Reset()
    {
        _last.Clear();
    }
}

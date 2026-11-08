using System.Numerics;
using Content.Client.Construction;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Watchers;
using Content.Shared.Popups;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Tutorial;

/// <summary>
/// Construction ghosts never exist on the server, so the current step cannot see them by itself.
/// This watches the client's ghosts against the step's markers and tells the server when that changes.
/// A ghost of the wrong recipe on a marker the step watches for one cannot be built.
/// </summary>
public sealed partial class TutorialConstructionGhostSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly List<ConstructionGhostOnAnchorCondition> _wanted = new();
    private readonly Dictionary<string, (bool OnTile, bool Stray, bool Wrong)> _last = new();
    private readonly HashSet<string> _seen = new();
    private string? _step;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Use,
                new PointerInputCmdHandler(OnUse, outsidePrediction: true),
                typeof(ConstructionSystem))
            .Register<TutorialConstructionGhostSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<TutorialConstructionGhostSystem>();
        base.Shutdown();
    }

    private bool OnUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (!TryComp<ConstructionGhostComponent>(args.EntityUid, out var ghost)
            || Transform(args.EntityUid) is not { GridUid: { } grid } xform
            || !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            return false;
        }

        foreach (var cond in _wanted)
        {
            if (!cond.Wrong
                || FindAnchor(grid, cond.AnchorId) is not { } anchor
                || !IsWrongOnTile(grid, gridComp, cond, anchor, ghost, xform))
            {
                continue;
            }

            _popup.PopupEntity(Loc.GetString("tutorial-craft-guide-wrong-ghost"), args.EntityUid);
            return true;
        }

        return false;
    }

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

        // the server drops its flags on every new step, so whatever stands on the markers is sent again
        if (_step != stepId.Id)
        {
            _step = stepId.Id;
            _last.Clear();
        }

        _wanted.Clear();
        Collect(step.Completion);
        CollectWatchers(step.Watchers);
        foreach (var setId in step.WatcherSets)
        {
            if (_proto.TryIndex(setId, out var set))
                CollectWatchers(set.Watchers);
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

            var state = Measure(grid, gridComp, cond, WrongConditionFor(cond.AnchorId));

            if (_last.TryGetValue(cond.AnchorId, out var prev) && prev == state)
                continue;

            _last[cond.AnchorId] = state;
            RaiseNetworkEvent(new TutorialConstructionGhostStateEvent(cond.AnchorId, state.OnTile, state.Stray, state.Wrong));
        }
    }

    private ConstructionGhostOnAnchorCondition? WrongConditionFor(string anchorId)
    {
        foreach (var cond in _wanted)
        {
            if (cond.Wrong && cond.AnchorId == anchorId)
                return cond;
        }

        return null;
    }

    private (bool OnTile, bool Stray, bool Wrong) Measure(
        EntityUid grid,
        MapGridComponent gridComp,
        ConstructionGhostOnAnchorCondition cond,
        ConstructionGhostOnAnchorCondition? wrongCond)
    {
        if (FindAnchor(grid, cond.AnchorId) is not { } anchor)
            return (false, false, false);

        var anchorTile = _map.TileIndicesFor(grid, gridComp, Transform(anchor).Coordinates);
        var anchorPos = _transform.GetWorldPosition(anchor);
        var recipe = cond.Recipe is { } recipeId ? recipeId.Id : null;
        var onTile = false;
        var stray = false;
        var wrong = false;

        var query = EntityQueryEnumerator<ConstructionGhostComponent, TransformComponent>();
        while (query.MoveNext(out _, out var ghost, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            if (wrongCond != null && IsWrongOnTile(grid, gridComp, wrongCond, anchor, ghost, xform))
                wrong = true;

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

        return (onTile, stray, wrong);
    }

    // only the marker's own tile counts: the build markers sit a tile apart, so anything wider
    // would call the right outline for the neighbouring marker wrong
    private bool IsWrongOnTile(
        EntityUid grid,
        MapGridComponent gridComp,
        ConstructionGhostOnAnchorCondition cond,
        EntityUid anchor,
        ConstructionGhostComponent ghost,
        TransformComponent xform)
    {
        if (cond.Recipe is not { } recipe || ghost.Prototype == null || ghost.Prototype.ID == recipe.Id)
            return false;

        return _map.TileIndicesFor(grid, gridComp, xform.Coordinates)
            == _map.TileIndicesFor(grid, gridComp, Transform(anchor).Coordinates);
    }

    private void CollectWatchers(List<TutorialWatcher> watchers)
    {
        foreach (var watcher in watchers)
        {
            if (watcher is ConditionWatcher cond)
                Collect(cond.Condition);
        }
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
        _step = null;
        _wanted.Clear();
        _last.Clear();
    }
}

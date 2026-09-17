using System.Numerics;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// A patient belongs to his room. Whatever drags him into a doorway - his own collar or the bag he
/// lies in - lets go there and he is back on the last spot inside.
/// </summary>
public sealed partial class TutorialConfinementSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public void Confine(EntityUid uid, List<EntityUid> doorways, LocId? popup)
    {
        var confined = EnsureComp<TutorialConfinedComponent>(uid);
        confined.Doorways = doorways;
        confined.Popup = popup;
        confined.LastInside = null;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TutorialConfinedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var confined, out var xform))
        {
            // the bag or locker he is in is what gets dragged
            var carrier = _container.TryGetOuterContainer(uid, xform, out var outer) ? outer.Owner : uid;

            // someone holding or wearing him is not a case this room has
            if (carrier != uid && HasComp<MobStateComponent>(carrier))
                continue;

            var carrierXform = Transform(carrier);
            if (!InDoorway(carrierXform, confined.Doorways))
            {
                confined.LastInside = carrierXform.Coordinates;
                continue;
            }

            if (confined.LastInside is not { } back)
                continue;

            if (TryComp<PullableComponent>(carrier, out var pullable) && pullable.Puller is { } puller)
            {
                _pulling.TryStopPull(carrier, pullable);
                if (confined.Popup is { } popup)
                    _popup.PopupEntity(Loc.GetString(popup), puller, puller, PopupType.SmallCaution);
            }

            _transform.SetCoordinates(carrier, back);
        }
    }

    private bool InDoorway(TransformComponent xform, List<EntityUid> doorways)
    {
        foreach (var door in doorways)
        {
            if (TerminatingOrDeleted(door))
                continue;

            var doorXform = Transform(door);
            if (doorXform.MapID != xform.MapID)
                continue;

            if (Tile(xform) == Tile(doorXform))
                return true;
        }

        return false;
    }

    /// <summary>Which tile it stands on, or the world square it is in when it is off-grid.</summary>
    private Vector2i Tile(TransformComponent xform)
    {
        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
            return _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

        var world = _transform.GetWorldPosition(xform);
        return new Vector2i((int) MathF.Floor(world.X), (int) MathF.Floor(world.Y));
    }
}

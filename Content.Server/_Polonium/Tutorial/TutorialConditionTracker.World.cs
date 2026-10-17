using System.Linq;
using System.Numerics;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Paper;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// The anchors themselves: where they stand, what state they are in and how badly they are
/// hurt. An anchor id can resolve to several entities, so most of this asks "any of them".
/// </summary>
public sealed partial class TutorialConditionTracker
{
    [Dependency] private MachineFrameSystem _machineFrame = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedMapSystem _map = default!;

    /// <summary>
    /// Every construction stage is a new entity, so tag whatever stands at the anchor right now and let
    /// an examine of any tagged one count. The flag is per step, the tags are simply left behind.
    /// </summary>
    private bool CheckExaminedNear(EntityUid player, TutorialSessionComponent session, ExaminedNearAnchorCondition cond)
    {
        var flag = $"examined-near:{cond.AnchorId}";
        if (session.Flags.Contains(flag))
            return true;

        foreach (var anchor in AnchorsOnGrid(player, cond.AnchorId))
        {
            foreach (var uid in _lookup.GetEntitiesInRange(Transform(anchor).Coordinates, cond.Range))
            {
                if (MetaData(uid).EntityPrototype?.ID is not { } id || !cond.Prototypes.Any(proto => proto.Id == id))
                    continue;

                EnsureComp<TutorialExamineProbeComponent>(uid).Flag = flag;
            }

            break;
        }

        return false;
    }

    private bool CheckFrameComplete(EntityUid player, MachineFrameCompleteNearAnchorCondition cond)
    {
        foreach (var anchor in AnchorsOnGrid(player, cond.AnchorId))
        {
            foreach (var uid in _lookup.GetEntitiesInRange(Transform(anchor).Coordinates, cond.Range))
            {
                if (TryComp<MachineFrameComponent>(uid, out var frame) && _machineFrame.IsComplete(frame))
                    return true;
            }

            break;
        }

        return false;
    }

    private bool CheckReach(EntityUid player, string anchorId, float range)
    {
        if (!TryComp(player, out TransformComponent? playerXform) || playerXform.GridUid is not { } grid)
            return false;

        var playerPos = _transform.GetWorldPosition(playerXform);
        var rangeSq = range * range;

        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var anchor, out var anchorXform))
        {
            if (anchor.AnchorId != anchorId || anchorXform.GridUid != grid)
                continue;

            if ((_transform.GetWorldPosition(anchorXform) - playerPos).LengthSquared() <= rangeSq)
                return true;
        }

        return false;
    }

    private bool CheckDeleted(TutorialSessionComponent session, string anchorId)
    {
        if (!session.Anchors.TryGetValue(anchorId, out var uid))
            return false;

        return Deleted(uid);
    }

    private bool CheckDamaged(EntityUid player, TutorialSessionComponent session, string anchorId)
    {
        if (session.Flags.Contains($"damaged:{anchorId}"))
            return true;

        if (session.Anchors.TryGetValue(anchorId, out var uid) && Deleted(uid))
            return true;

        return AnyAnchor(player, anchorId, HasAnyDamage);
    }

    private bool HasAnyDamage(EntityUid uid)
    {
        return TryComp<DamageableComponent>(uid, out var dmg)
               && _damageable.GetPositiveDamage((uid, dmg)).AnyPositive();
    }

    private bool CheckEmptyOrGone(TutorialSessionComponent session, string anchorId)
    {
        if (!session.Anchors.TryGetValue(anchorId, out var uid) || Deleted(uid))
            return true;

        var volume = 0f;
        var any = false;
        foreach (var (_, solutionEnt) in _solution.EnumerateSolutions((uid, null)))
        {
            any = true;
            volume += solutionEnt.Comp.Solution.Volume.Float();
        }

        return any && volume <= 0.01f;
    }

    private bool BuckledTo(EntityUid who, string? strapAnchorId)
    {
        if (!TryComp<BuckleComponent>(who, out var buckle) || !buckle.Buckled || buckle.BuckledTo is not { } strap)
            return false;

        if (strapAnchorId is null)
            return TryComp<StrapComponent>(strap, out var lying) && lying.Position == StrapPosition.Down;

        return TryComp<TutorialAnchorComponent>(strap, out var anchor) && anchor.AnchorId == strapAnchorId;
    }

    private float DamageOf(EntityUid uid, ProtoId<DamageTypePrototype>? type)
    {
        if (!TryComp<DamageableComponent>(uid, out var dmg))
            return 0f;

        var positive = _damageable.GetPositiveDamage((uid, dmg));
        if (type is not { } only)
            return positive.GetTotal().Float();

        return positive.DamageDict.TryGetValue(only.Id, out var amount)
            ? amount.Float()
            : 0f;
    }

    private (EntityUid, Vector2i)? TileOf(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return null;

        return (grid, _map.TileIndicesFor(grid, gridComp, xform.Coordinates));
    }

    // the server moves the view on the input command too, so this needs no word from the client
    private bool CheckCameraRotated(EntityUid player, TutorialSessionComponent session, CameraRotatedCondition cond)
    {
        if (!TryComp<InputMoverComponent>(player, out var mover))
            return false;

        // rotate-right adds a negative quarter turn
        var delta = Angle.ShortestDistance(session.CameraAtStepStart, mover.TargetRelativeRotation).Degrees;
        var size = Math.Abs(delta);
        if (size < cond.Degrees - 0.5 || size > cond.MaxDegrees + 0.5)
            return false;

        return cond.Direction switch
        {
            TutorialRotation.Right => delta < 0,
            TutorialRotation.Left => delta > 0,
            _ => true,
        };
    }

    private bool CheckAnchorsNear(EntityUid player, string a, string b, float range, bool away = false)
    {
        var closest = float.MaxValue;

        foreach (var left in AnchorsOnGrid(player, a))
        foreach (var right in AnchorsOnGrid(player, b))
        {
            var delta = _transform.GetWorldPosition(left) - _transform.GetWorldPosition(right);
            closest = MathF.Min(closest, delta.LengthSquared());
        }

        // neither "near" nor "away" means anything when one of the two is gone
        if (closest is float.MaxValue)
            return false;

        var rangeSq = range * range;
        return away ? closest > rangeSq : closest <= rangeSq;
    }

    private bool AnyAnchor(EntityUid player, string anchorId, Func<EntityUid, bool> pred)
    {
        foreach (var uid in AnchorsOnGrid(player, anchorId))
        {
            if (pred(uid))
                return true;
        }

        return false;
    }

    private IEnumerable<EntityUid> AnchorsOnGrid(EntityUid player, string anchorId)
    {
        if (!TryComp(player, out TransformComponent? xform) || xform.GridUid is not { } grid)
            yield break;

        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var ax))
        {
            if (anchor.AnchorId == anchorId && ax.GridUid == grid && !Deleted(uid))
                yield return uid;
        }
    }

    private bool IsSigned(EntityUid uid)
    {
        return TryComp<PaperComponent>(uid, out var paper) && paper.StampedBy.Count > 0;
    }
}

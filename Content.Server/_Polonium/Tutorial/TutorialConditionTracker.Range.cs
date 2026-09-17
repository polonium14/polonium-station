using System.Numerics;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// The shooting range: which target the trainee aims at, how many shots went where, and how
/// many of them landed.
/// </summary>
public sealed partial class TutorialConditionTracker
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public const string WrongTargetFlag = "drill-wrong-target";

    // the last bullet is still in the air when the last shot is counted, give it time to land
    private static readonly TimeSpan DrillSettle = TimeSpan.FromSeconds(1);

    private const float AimedAnchorRange = 0.75f;

    private readonly HashSet<Entity<TutorialAnchorComponent>> _aimedAnchors = new();

    private void InitializeRange()
    {
        SubscribeLocalEvent<TutorialAnchorComponent, GunShotEvent>(OnAnchorGunShot);
        SubscribeLocalEvent<TutorialAnchorComponent, OnEmptyGunShotEvent>(OnAnchorEmptyShot);
        SubscribeLocalEvent<TutorialAnchorComponent, HitscanRaycastStrikeEvent>(OnAnchorHitscan);
    }

    public void PrimeDrill(EntityUid player, TutorialSessionComponent session, TutorialStepPrototype step)
    {
        if (FindDrill(step.Completion) is { } drill)
            UpdateDrillFocus(player, session, drill);
    }

    private void OnAnchorGunShot(Entity<TutorialAnchorComponent> gun, ref GunShotEvent args)
    {
        var user = args.User;
        if (!TryComp<TutorialSessionComponent>(user, out var session))
            return;

        FlagAimedAnchors(user, args.ToCoordinates);

        if (_tutorial.TryGetCurrentStep(session, out _, out var step)
            && FindDrill(step.Completion) is { } drill
            && args.Ammo.Count > 0)
        {
            CountDrillShot(user, session, drill, gun, args.Ammo.Count, args.ToCoordinates);
        }

        Notify(user);
    }

    private void OnAnchorEmptyShot(Entity<TutorialAnchorComponent> gun, ref OnEmptyGunShotEvent args)
    {
        if (!HasComp<TutorialSessionComponent>(args.User)
            || !TryComp<GunComponent>(gun, out var gunComp)
            || gunComp.ShootCoordinates is not { } aimedAt)
            return;

        FlagAimedAnchors(args.User, aimedAt);
        Notify(args.User);
    }

    /// <summary>
    /// "fired-at:id" for every anchor the click landed on, so a step can tell a gun clicked at the
    /// charger from one clicked at the targets.
    /// </summary>
    private void FlagAimedAnchors(EntityUid user, EntityCoordinates aimedAt)
    {
        _aimedAnchors.Clear();
        _lookup.GetEntitiesInRange(_transform.ToMapCoordinates(aimedAt), AimedAnchorRange, _aimedAnchors);

        foreach (var anchor in _aimedAnchors)
            SetFlag(user, $"fired-at:{anchor.Comp.AnchorId}");
    }

    private void OnAnchorHitscan(Entity<TutorialAnchorComponent> target, ref HitscanRaycastStrikeEvent args)
    {
        if (args.Data.Shooter is { } shooter)
            CountDrillHit(shooter, target);
    }

    private void CountDrillShot(
        EntityUid user,
        TutorialSessionComponent session,
        ShootTargetsCondition drill,
        EntityUid gun,
        int shots,
        EntityCoordinates aimedAt)
    {
        session.DrillShots += shots;
        session.LastDrillShot = _timing.CurTime;

        if (AimedTarget(user, gun, drill, aimedAt) is not { } aimed)
            return;

        UpdateDrillFocus(user, session, drill);
        var focus = session.FocusTarget;
        if (aimed != focus)
        {
            SetFlag(user, WrongTargetFlag);
            return;
        }

        session.TargetShots[aimed] = session.TargetShots.GetValueOrDefault(aimed) + shots;
        UpdateDrillFocus(user, session, drill);

        if (session.FocusTarget is { } next && next != focus)
            _popup.PopupEntity(Loc.GetString("tutorial-range-next-target"), next, user);
    }

    private void CountDrillHit(EntityUid shooter, EntityUid target)
    {
        if (!TryComp<TutorialSessionComponent>(shooter, out var session)
            || !TryComp<TutorialAnchorComponent>(target, out var anchor)
            || !_tutorial.TryGetCurrentStep(session, out _, out var step)
            || FindDrill(step.Completion) is not { } drill
            || drill.AnchorId != anchor.AnchorId)
            return;

        session.DrillHits++;
    }

    private bool CheckShootTargets(EntityUid player, TutorialSessionComponent session, ShootTargetsCondition drill)
    {
        if (!UpdateDrillFocus(player, session, drill) || session.FocusTarget != null)
            return false;

        return _timing.CurTime - session.LastDrillShot >= DrillSettle;
    }

    /// <summary>Points the focus at the first target still short of shots. False if the row is missing.</summary>
    private bool UpdateDrillFocus(EntityUid player, TutorialSessionComponent session, ShootTargetsCondition drill)
    {
        var order = DrillOrder(player, drill);

        EntityUid? focus = null;
        foreach (var target in order)
        {
            if (session.TargetShots.GetValueOrDefault(target) >= drill.ShotsEach)
                continue;

            focus = target;
            break;
        }

        if (session.FocusTarget != focus)
        {
            session.FocusTarget = focus;
            // finished row: nothing is left to point at, so none of it glows
            if (focus == null)
                session.HighlightAnchors.Remove(drill.AnchorId);

            Dirty(player, session);
        }

        return order.Count > 0;
    }

    private List<EntityUid> DrillOrder(EntityUid player, ShootTargetsCondition drill)
    {
        var origin = _transform.GetWorldPosition(player);
        if (drill.OrderFrom is { } from)
        {
            foreach (var anchor in AnchorsOnGrid(player, from))
            {
                origin = _transform.GetWorldPosition(anchor);
                break;
            }
        }

        var targets = new List<(EntityUid Uid, float Distance)>();
        foreach (var target in AnchorsOnGrid(player, drill.AnchorId))
            targets.Add((target, (_transform.GetWorldPosition(target) - origin).LengthSquared()));

        targets.Sort((a, b) => a.Distance != b.Distance
            ? a.Distance.CompareTo(b.Distance)
            : a.Uid.CompareTo(b.Uid));

        return targets.ConvertAll(t => t.Uid);
    }

    private EntityUid? AimedTarget(EntityUid player, EntityUid gun, ShootTargetsCondition drill, EntityCoordinates aimedAt)
    {
        // a click right on the sprite names the target outright
        if (TryComp<GunComponent>(gun, out var gunComp)
            && gunComp.Target is { } clicked
            && TryComp<TutorialAnchorComponent>(clicked, out var clickedAnchor)
            && clickedAnchor.AnchorId == drill.AnchorId)
            return clicked;

        var aim = _transform.ToMapCoordinates(aimedAt);
        EntityUid? best = null;
        var bestDistance = drill.AimRange * drill.AimRange;

        foreach (var target in AnchorsOnGrid(player, drill.AnchorId))
        {
            var pos = _transform.GetMapCoordinates(target);
            if (pos.MapId != aim.MapId)
                continue;

            var distance = Vector2.DistanceSquared(pos.Position, aim.Position);
            if (distance > bestDistance)
                continue;

            best = target;
            bestDistance = distance;
        }

        return best;
    }

    private bool IsWielded(EntityUid item)
    {
        return TryComp<WieldableComponent>(item, out var wield) && wield.Wielded;
    }

    private static ShootTargetsCondition? FindDrill(TutorialCondition? condition)
    {
        switch (condition)
        {
            case ShootTargetsCondition drill:
                return drill;
            case AllCondition all:
                foreach (var inner in all.Conditions)
                {
                    if (FindDrill(inner) is { } found)
                        return found;
                }
                return null;
            case AnyCondition any:
                foreach (var inner in any.Conditions)
                {
                    if (FindDrill(inner) is { } found)
                        return found;
                }
                return null;
            default:
                return null;
        }
    }
}

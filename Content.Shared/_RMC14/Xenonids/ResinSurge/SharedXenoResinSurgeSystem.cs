using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.FloorResin;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using System.Numerics;

namespace Content.Shared._RMC14.Xenonids.ResinSurge;

public sealed partial class SharedXenoResinSurgeSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SharedMapSystem _sharedMap = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedXenoWeedsSystem _weeds = default!;
    [Dependency] private XenoPlasmaSystem _plasma = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoResinSurgeComponent, XenoResinSurgeActionEvent>(OnResinSurge);
        SubscribeLocalEvent<XenoResinSurgeComponent, ResinSurgeStickyResinDoafter>(OnStickyDoAfter);
    }

    private void OnResinSurge(Entity<XenoResinSurgeComponent> xeno, ref XenoResinSurgeActionEvent args)
    {
        if (args.Handled)
            return;

        if (xeno.Comp.ResinDoAfter != null)
            return;

        var target = _rmcMap.SnapToGrid(args.Target);
        if (_transform.GetGrid(target) is not { } gridId ||
            !TryComp(gridId, out MapGridComponent? _))
            return;

        if (!_transform.InRange(Transform(xeno).Coordinates, target, xeno.Comp.Range))
        {
            _popup.PopupClient(Loc.GetString("rmc-xeno-resin-surge-see-fail"), xeno, xeno);
            return;
        }

        // reinforce not ported - dont burn plasma on construct clicks
        if (args.Entity is { } entity && HasComp<XenoConstructComponent>(entity))
            return;

        var cost = args.PlasmaCost != FixedPoint2.Zero ? args.PlasmaCost : xeno.Comp.PlasmaCost;
        if (!_plasma.HasPlasmaPopup(xeno.Owner, cost))
            return;

        // unstable wall only when clicking friendly weeds directly - not every weeded tile
        if (args.Entity is { } clicked)
        {
            EntityUid weedEnt = clicked;
            XenoWeedsComponent? weeds = null;

            if (TryComp(clicked, out XenoWeedsComponent? weedComp))
                weeds = weedComp;
            else if (_weeds.IsOnFriendlyWeeds(clicked))
            {
                var weedOnFloor = _weeds.GetWeedsOnFloor(Transform(clicked).Coordinates);
                if (weedOnFloor != null)
                {
                    weedEnt = weedOnFloor.Value;
                    TryComp(weedEnt, out weeds);
                }
            }

            if (weeds != null && _hive.FromSameHive(xeno.Owner, weedEnt) && _rmcMap.CanBuildOn(target))
            {
                if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, cost))
                    return;

                args.Handled = true;

                if (_net.IsServer)
                {
                    var wall = Spawn(xeno.Comp.UnstableWallId, target);
                    EnsureComp<XenoConstructComponent>(wall);
                    _hive.SetSameHive(xeno.Owner, wall);
                }

                SetSurgeCooldown(xeno);
                return;
            }
        }

        if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, cost))
            return;

        var ev = new ResinSurgeStickyResinDoafter(GetNetCoordinates(target), cost);
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.StickyResinDoAfterPeriod, ev, xeno)
        {
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out var id))
        {
            _plasma.RegenPlasma(xeno.Owner, cost);
            return;
        }

        xeno.Comp.ResinDoAfter = id;
        args.Handled = true;
    }

    private void OnStickyDoAfter(Entity<XenoResinSurgeComponent> xeno, ref ResinSurgeStickyResinDoafter args)
    {
        xeno.Comp.ResinDoAfter = null;

        if (args.Cancelled)
        {
            _plasma.RegenPlasma(xeno.Owner, args.PlasmaCost);
            return;
        }

        var coords = GetCoordinates(args.Coordinates);
        if (_transform.GetGrid(coords) is not { } gridId ||
            !TryComp(gridId, out MapGridComponent? grid))
            return;

        if (_net.IsServer)
        {
            var size = xeno.Comp.StickyResinRadius * 2;
            foreach (var turf in _sharedMap.GetTilesIntersecting(
                         gridId,
                         grid,
                         Box2.CenteredAround(coords.Position, new(size, size)),
                         false))
            {
                var center = _turf.GetTileCenter(turf);
                if (!_rmcMap.CanBuildOn(center) && !_weeds.IsOnWeeds(center))
                    continue;

                if (_rmcMap.HasAnchoredEntityEnumerator<XenoStickyResinComponent>(center))
                    continue;

                var resin = Spawn(xeno.Comp.StickyResinId, center);
                _hive.SetSameHive(xeno.Owner, resin);
            }
        }

        SetSurgeCooldown(xeno);
    }

    private void SetSurgeCooldown(Entity<XenoResinSurgeComponent> xeno, TimeSpan? cooldown = null)
    {
        var cd = cooldown ?? xeno.Comp.SuccessCooldown;
        foreach (var action in _actions.GetActions(xeno))
        {
            if (!TryComp(action, out WorldTargetActionComponent? world) ||
                world.Event is not XenoResinSurgeActionEvent)
                continue;

            _actions.SetCooldown((action, action), cd);
            return;
        }
    }
}

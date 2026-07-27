using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Construction.Events;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Construction;

public sealed partial class SharedXenoConstructionSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private XenoPlasmaSystem _plasma = default!;
    [Dependency] private SharedXenoWeedsSystem _weeds = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoConstructionComponent, XenoPlantWeedsActionEvent>(OnPlantWeeds);
        SubscribeLocalEvent<XenoConstructionComponent, XenoChooseStructureActionEvent>(OnChooseStructure);
        SubscribeLocalEvent<XenoConstructionComponent, XenoSecreteStructureActionEvent>(OnSecreteStructure);
        SubscribeLocalEvent<XenoConstructionComponent, XenoSecreteStructureDoAfterEvent>(OnSecreteStructureDoAfter);

        Subs.BuiEvents<XenoConstructionComponent>(XenoChooseStructureUI.Key, subs =>
        {
            subs.Event<XenoChooseStructureMessage>(OnChooseStructureMessage);
        });
    }

    private void OnPlantWeeds(Entity<XenoConstructionComponent> xeno, ref XenoPlantWeedsActionEvent args)
    {
        if (args.Handled)
            return;

        var coordinates = _rmcMap.SnapToGrid(Transform(xeno).Coordinates);
        if (_transform.GetGrid(coordinates) == null)
            return;

        if (_weeds.IsOnWeeds(coordinates))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-weeds-source-already-here"), xeno, xeno);
            return;
        }

        if (!_rmcMap.CanBuildOn(coordinates))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-cant-build-here"), xeno, xeno);
            return;
        }

        var cost = args.PlasmaCost != FixedPoint2.Zero ? args.PlasmaCost : xeno.Comp.PlantWeedsCost;
        if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, cost))
            return;

        args.Handled = true;

        var proto = args.Prototype ?? xeno.Comp.WeedPrototype;
        if (_net.IsServer)
        {
            var weeds = Spawn(proto, coordinates);
            _hive.SetSameHive(xeno.Owner, weeds);
        }

        _audio.PlayPredicted(xeno.Comp.BuildSound, coordinates, xeno);
    }

    private void OnChooseStructure(Entity<XenoConstructionComponent> xeno, ref XenoChooseStructureActionEvent args)
    {
        args.Handled = true;
        _ui.TryOpenUi(xeno.Owner, XenoChooseStructureUI.Key, xeno);
    }

    private void OnChooseStructureMessage(Entity<XenoConstructionComponent> xeno, ref XenoChooseStructureMessage args)
    {
        if (!xeno.Comp.CanBuild.Contains(args.StructureId))
            return;

        xeno.Comp.SelectedStructure = args.StructureId;
        Dirty(xeno);
    }

    private void OnSecreteStructure(Entity<XenoConstructionComponent> xeno, ref XenoSecreteStructureActionEvent args)
    {
        if (args.Handled)
            return;

        var coordinates = _rmcMap.SnapToGrid(args.Target);
        if (_transform.GetGrid(coordinates) == null)
            return;

        if (!_transform.InRange(Transform(xeno).Coordinates, coordinates, xeno.Comp.BuildRange.Float()))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-cant-reach-there"), xeno, xeno);
            return;
        }

        if (xeno.Comp.CanUpgrade &&
            _rmcMap.HasAnchoredEntityEnumerator<XenoStructureUpgradeableComponent>(coordinates, out var upgradeable))
        {
            if (!_weeds.IsOnWeeds(coordinates))
            {
                _popup.PopupClient(Loc.GetString("cm-xeno-construction-failed-need-weeds"), coordinates, xeno);
                return;
            }

            if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, upgradeable.Comp.Cost))
                return;

            args.Handled = true;
            if (_net.IsServer)
            {
                var coords = Transform(upgradeable).Coordinates;
                var upgradeTo = upgradeable.Comp.To;
                QueueDel(upgradeable);
                var upgraded = Spawn(upgradeTo, coords);
                EnsureComp<XenoConstructComponent>(upgraded);
                _hive.SetSameHive(xeno.Owner, upgraded);
            }

            _audio.PlayPredicted(xeno.Comp.BuildSound, coordinates, xeno);
            return;
        }

        if (xeno.Comp.SelectedStructure is not { } choice || !xeno.Comp.CanBuild.Contains(choice))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-construction-failed-select-structure"), xeno, xeno);
            return;
        }

        if (!CanSecreteOnTile(xeno, coordinates, choice, checkPlasma: true))
            return;

        args.Handled = true;
        var ev = new XenoSecreteStructureDoAfterEvent(GetNetCoordinates(coordinates), choice);
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.BuildDelay, ev, xeno)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnSecreteStructureDoAfter(Entity<XenoConstructionComponent> xeno, ref XenoSecreteStructureDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        var coordinates = GetCoordinates(args.Coordinates);
        if (!coordinates.IsValid(EntityManager) ||
            !xeno.Comp.CanBuild.Contains(args.StructureId) ||
            !CanSecreteOnTile(xeno, coordinates, args.StructureId, checkPlasma: false))
            return;

        if (GetStructurePlasmaCost(args.StructureId) is { } cost &&
            !_plasma.TryRemovePlasmaPopup(xeno.Owner, cost))
            return;

        args.Handled = true;

        if (_net.IsServer)
        {
            var structure = Spawn(args.StructureId, coordinates);
            EnsureComp<XenoConstructComponent>(structure);
            _hive.SetSameHive(xeno.Owner, structure);
        }

        _audio.PlayPredicted(xeno.Comp.BuildSound, coordinates, xeno);
    }

    private bool CanSecreteOnTile(
        Entity<XenoConstructionComponent> xeno,
        EntityCoordinates coordinates,
        EntProtoId choice,
        bool checkPlasma)
    {
        if (!_weeds.IsOnWeeds(coordinates))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-construction-failed-need-weeds"), coordinates, xeno);
            return false;
        }

        if (!_rmcMap.CanBuildOn(coordinates) ||
            _rmcMap.HasAnchoredEntityEnumerator<XenoConstructComponent>(coordinates))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-cant-build-here"), xeno, xeno);
            return false;
        }

        if (checkPlasma &&
            GetStructurePlasmaCost(choice) is { } cost &&
            !_plasma.HasPlasmaPopup(xeno.Owner, cost))
            return false;

        return true;
    }

    public FixedPoint2? GetStructurePlasmaCost(EntProtoId prototype)
    {
        if (!_prototype.TryIndex(prototype, out var proto))
            return null;

        if (!proto.TryGetComponent(out XenoConstructionPlasmaCostComponent? cost, _compFactory))
            return null;

        return cost.Plasma;
    }
}

// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.AlertLevel;
using Content.Server.Silicons.Laws;
using Content.Server.Wires;
using Content.Shared._Polonium.StationAi;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Ghost;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.StationAi;

/// <summary>
/// Handles Code Epsilon AI law changes, the external lockdown action, and station-wide external airlock bolting.
/// </summary>
public sealed partial class ExternalLockdownSystem : EntitySystem
{
    public static readonly ProtoId<SiliconLawsetPrototype> LastInstructionsLawset = "LastInstructions";
    public static readonly EntProtoId AiLockdownActionId = "ActionStationAiExternalLockdown";
    public static readonly EntProtoId AghostLockdownActionId = "ActionAGhostExternalLockdown";
    private static readonly EntProtoId AdminObserverPrototypeId = "AdminObserver";
    private static readonly ProtoId<AccessLevelPrototype> ExternalAccess = "External";
    private static readonly ProtoId<WireLayoutPrototype> ExternalWireLayout = "AirlockExternal";
    private const string EpsilonAlertLevel = "epsilon";

    private readonly HashSet<EntityUid> _stationsInEpsilon = new();

    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SiliconLawSystem _laws = default!;
    [Dependency] private SharedStationSystem _station = default!;

    [Dependency] private EntityQuery<FirelockComponent> _firelockQuery = default!;
    [Dependency] private EntityQuery<DoorBoltComponent> _boltQuery = default!;
    [Dependency] private EntityQuery<WiresComponent> _wiresQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<StationAiExternalLockdownEvent>(OnLockdownAction);
        SubscribeLocalEvent<StationAiCoreComponent, MapInitEvent>(OnStationAiCoreMapInit);
        SubscribeLocalEvent<BypassInteractionChecksComponent, MapInitEvent>(OnAdminObserverMapInit); // This component exists only in aghost, god please don't let this component be added to any other entity
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        if (args.AlertLevel == EpsilonAlertLevel)
        {
            _stationsInEpsilon.Add(args.Station);
            ApplyEpsilonToStationAis(args.Station);
            GrantLockdownToAdminObservers();
            return;
        }

        if (!_stationsInEpsilon.Remove(args.Station))
            return;

        ClearEpsilonFromStationAis(args.Station);

        if (_stationsInEpsilon.Count == 0)
            RevokeLockdownFromAdminObservers();
    }

    private void OnStationAiCoreMapInit(Entity<StationAiCoreComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out TransformComponent? xform))
            return;

        var station = _station.GetOwningStation(ent, xform);
        if (station == null || !_stationsInEpsilon.Contains(station.Value))
            return;

        ApplyEpsilonToCoreAis(ent);
    }

    private void OnAdminObserverMapInit(EntityUid uid, BypassInteractionChecksComponent _, ref MapInitEvent args)
    {
        if (_stationsInEpsilon.Count == 0)
            return;

        EnsureLockdownAction(uid, AghostLockdownActionId);
    }

    private void OnLockdownAction(StationAiExternalLockdownEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var station = _station.GetOwningStation(args.Performer);
        if (station == null)
        {
            _popup.PopupEntity(Loc.GetString("station-ai-external-lockdown-no-station"), args.Performer, args.Performer);
            return;
        }

        var count = BoltExternalAirlocksOnStation(station.Value);

        _popup.PopupEntity(
            Loc.GetString("station-ai-external-lockdown-complete", ("count", count)),
            args.Performer,
            args.Performer);
    }

    public void ApplyEpsilonToStationAis(EntityUid station)
    {
        var query = EntityQueryEnumerator<StationAiCoreComponent, TransformComponent>();
        while (query.MoveNext(out var coreUid, out _, out var xform))
        {
            if (_station.GetOwningStation(coreUid, xform) != station)
                continue;

            ApplyEpsilonToCoreAis(coreUid);
        }
    }

    private void ApplyEpsilonToCoreAis(EntityUid coreUid)
    {
        if (!_container.TryGetContainer(coreUid, StationAiCoreComponent.Container, out var container))
            return;

        foreach (var ai in container.ContainedEntities)
        {
            ApplyLastInstructions(ai);
            EnsureLockdownAction(ai, AiLockdownActionId);
        }
    }
    public void ClearEpsilonFromStationAis(EntityUid station)
    {
        var query = EntityQueryEnumerator<StationAiCoreComponent, TransformComponent>();
        while (query.MoveNext(out var coreUid, out _, out var xform))
        {
            if (_station.GetOwningStation(coreUid, xform) != station)
                continue;

            if (!_container.TryGetContainer(coreUid, StationAiCoreComponent.Container, out var container))
                continue;

            foreach (var ai in container.ContainedEntities)
            {
                RestoreDefaultLaws(ai);
                RemoveLockdownAction(ai, AiLockdownActionId);
            }
        }
    }

    private void GrantLockdownToAdminObservers()
    {
        var query = EntityQueryEnumerator<GhostComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!IsAdminObserver(uid))
                continue;

            EnsureLockdownAction(uid, AghostLockdownActionId);
        }
    }

    private void RevokeLockdownFromAdminObservers()
    {
        var query = EntityQueryEnumerator<GhostComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!IsAdminObserver(uid))
                continue;

            RemoveLockdownAction(uid, AghostLockdownActionId);
        }
    }

    private bool IsAdminObserver(EntityUid uid)
    {
        return MetaData(uid).EntityPrototype?.ID == AdminObserverPrototypeId.Id;
    }

    public void ApplyLastInstructions(EntityUid silicon)
    {
        if (!TryComp<SiliconLawProviderComponent>(silicon, out var provider))
            return;

        if (!HasComp<EpsilonLawBackupComponent>(silicon))
        {
            var backup = EnsureComp<EpsilonLawBackupComponent>(silicon);
            backup.Lawset = _laws.GetLaws(silicon).Clone();
        }

        var lawset = _laws.GetLawset(LastInstructionsLawset);
        _laws.SetLaws(lawset.Laws, silicon, provider.LawUploadSound);
    }

    public void RestoreDefaultLaws(EntityUid silicon)
    {
        if (!TryComp<SiliconLawProviderComponent>(silicon, out var provider))
            return;

        if (!TryComp<EpsilonLawBackupComponent>(silicon, out var backup))
            return;

        _laws.SetLaws(backup.Lawset.Clone().Laws, silicon, provider.LawUploadSound);
        RemComp<EpsilonLawBackupComponent>(silicon);
    }

    public void EnsureLockdownAction(EntityUid entity, EntProtoId actionId)
    {
        foreach (var (actionUid, _) in _actions.GetActions(entity))
        {
            if (MetaData(actionUid).EntityPrototype?.ID == actionId.Id)
                return;
        }

        _actions.AddAction(entity, actionId);
    }

    public void RemoveLockdownAction(EntityUid entity, EntProtoId actionId)
    {
        foreach (var (actionUid, _) in _actions.GetActions(entity).ToList())
        {
            if (MetaData(actionUid).EntityPrototype?.ID == actionId.Id)
                _actions.RemoveAction(actionUid);
        }
    }

    public int BoltExternalAirlocksOnStation(EntityUid station)
    {
        var bolted = 0;
        var query = AllEntityQuery<AirlockComponent, DoorComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var door, out var xform))
        {
            if (_firelockQuery.HasComp(uid))
                continue;

            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != station)
                continue;

            if (!IsExternalAirlock(uid))
                continue;

            if (door.State != DoorState.Closed)
            {
                if (door.State is DoorState.Open or DoorState.Opening)
                    _doors.TryClose(uid, door);

                continue;
            }

            if (!_boltQuery.TryComp(uid, out var bolt))
                continue;

            if (_doors.TrySetBoltDown((uid, bolt), true))
                bolted++;
            else if (bolt.BoltsDown)
                bolted++;
        }

        return bolted;
    }

    private bool IsExternalAirlock(EntityUid uid)
    {
        if (_wiresQuery.TryComp(uid, out var wires) && wires.LayoutId == ExternalWireLayout)
            return true;

        if (!_access.GetMainAccessReader(uid, out var accessEnt))
            return false;

        return accessEnt.Value.Comp.AccessLists.Any(list => list.Contains(ExternalAccess));
    }
}

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
using Content.Shared.Emag.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Interaction.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.AlertLevel;
using Content.Shared.Ghost.Components;
using Content.Shared.Station.Systems;

namespace Content.Server._Polonium.StationAi;

/// <summary>
/// Handles Code Epsilon silicon law changes, the external lockdown action, and station-wide external airlock bolting.
/// </summary>
public sealed partial class ExternalLockdownSystem : EntitySystem
{
    public static readonly ProtoId<SiliconLawsetPrototype> Protocol66Lawset = "Protocol66";
    public static readonly ProtoId<SiliconLawsetPrototype> AccessErrorLawset = "Protocol66AccessError";
    public static readonly EntProtoId AiLockdownActionId = "ActionStationAiExternalLockdown";
    public static readonly EntProtoId AghostLockdownActionId = "ActionAGhostExternalLockdown";
    private static readonly EntProtoId AdminObserverPrototypeId = "AdminObserver";
    private static readonly ProtoId<AccessLevelPrototype> ExternalAccess = "External";
    private static readonly ProtoId<WireLayoutPrototype> ExternalWireLayout = "AirlockExternal";
    private static readonly ProtoId<AlertLevelPrototype> EpsilonAlertLevel = "Epsilon";
    private static readonly ProtoId<NpcFactionPrototype> NanoTrasenFaction = "NanoTrasen";

    private readonly HashSet<EntityUid> _stationsInEpsilon = new();

    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SiliconLawSystem _laws = default!;
    [Dependency] private StationSystem _station = default!;

    [Dependency] private EntityQuery<FirelockComponent> _firelockQuery = default!;
    [Dependency] private EntityQuery<DoorBoltComponent> _boltQuery = default!;
    [Dependency] private EntityQuery<WiresComponent> _wiresQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<StationAiExternalLockdownEvent>(OnLockdownAction);
        SubscribeLocalEvent<StationAiHeldComponent, MapInitEvent>(OnAiEnteredCore);
        SubscribeLocalEvent<StationAiHeldComponent, ComponentShutdown>(OnAiLeftCore);
        SubscribeLocalEvent<SiliconLawProviderComponent, MapInitEvent>(OnLawProviderMapInit);
        SubscribeLocalEvent<EpsilonLawBackupComponent, SiliconEmaggedEvent>(OnBackupEmagged, before: [typeof(SiliconLawSystem)]);
        SubscribeLocalEvent<BypassInteractionChecksComponent, MapInitEvent>(OnAdminObserverMapInit); // This component exists only in aghost, god please don't let this component be added to any other entity
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _stationsInEpsilon.Clear();
    }

    private void OnAlertLevelChanged(ref AlertLevelChangedEvent args)
    {
        if (args.AlertLevel == EpsilonAlertLevel)
        {
            _stationsInEpsilon.Add(args.Station);
            ApplyEpsilonToStationAis(args.Station);
            ApplyEpsilonToStationBorgs(args.Station);
            GrantLockdownToAdminObservers();
            return;
        }

        if (!_stationsInEpsilon.Remove(args.Station))
            return;

        ClearEpsilonFromStation(args.Station);

        if (_stationsInEpsilon.Count == 0)
            RevokeLockdownFromAdminObservers();
    }

    private void OnAiEnteredCore(Entity<StationAiHeldComponent> ent, ref MapInitEvent args)
    {
        if (_station.GetOwningStation(ent.Owner) is not { } station || !_stationsInEpsilon.Contains(station))
            return;

        ApplyEpsilonLaws(ent, station, Protocol66Lawset);
        EnsureLockdownAction(ent, AiLockdownActionId);
    }

    private void OnAiLeftCore(Entity<StationAiHeldComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            RemoveLockdownAction(ent, AiLockdownActionId);
    }

    private void OnLawProviderMapInit(Entity<SiliconLawProviderComponent> ent, ref MapInitEvent args)
    {
        if (_stationsInEpsilon.Count == 0 || !IsNanotrasenBorg(ent))
            return;

        if (_station.GetOwningStation(ent.Owner) is { } station && _stationsInEpsilon.Contains(station))
            ApplyEpsilonLaws(ent, station, AccessErrorLawset);
    }

    private void OnBackupEmagged(Entity<EpsilonLawBackupComponent> ent, ref SiliconEmaggedEvent args)
    {
        RestoreDefaultLaws(ent);
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

            if (!_container.TryGetContainer(coreUid, StationAiCoreComponent.Container, out var container))
                continue;

            foreach (var ai in container.ContainedEntities)
            {
                ApplyEpsilonLaws(ai, station, Protocol66Lawset);
                EnsureLockdownAction(ai, AiLockdownActionId);
            }
        }
    }

    private void ApplyEpsilonToStationBorgs(EntityUid station)
    {
        var query = EntityQueryEnumerator<BorgChassisComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (_station.GetOwningStation(uid, xform) == station && IsNanotrasenBorg(uid))
                ApplyEpsilonLaws(uid, station, AccessErrorLawset);
        }
    }

    private bool IsNanotrasenBorg(EntityUid uid)
    {
        return HasComp<BorgChassisComponent>(uid)
               && _faction.IsMember(uid, NanoTrasenFaction)
               && !_emag.CheckFlag(uid, EmagType.Interaction);
    }

    public void ClearEpsilonFromStation(EntityUid station)
    {
        var silicons = new List<EntityUid>();
        var query = EntityQueryEnumerator<EpsilonLawBackupComponent>();
        while (query.MoveNext(out var uid, out var backup))
        {
            if (backup.Station == station)
                silicons.Add(uid);
        }

        foreach (var silicon in silicons)
        {
            RestoreDefaultLaws(silicon);
            RemoveLockdownAction(silicon, AiLockdownActionId);
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

    private void ApplyEpsilonLaws(EntityUid silicon, EntityUid station, ProtoId<SiliconLawsetPrototype> lawset)
    {
        if (HasComp<EpsilonLawBackupComponent>(silicon)
            || !TryComp<SiliconLawProviderComponent>(silicon, out var provider))
            return;

        var backup = AddComp<EpsilonLawBackupComponent>(silicon);
        backup.Lawset = _laws.GetLaws(silicon).Clone();
        backup.Station = station;

        _laws.SetLaws(_laws.GetLawset(lawset).Laws, silicon, provider.LawUploadSound);
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

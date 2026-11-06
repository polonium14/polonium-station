using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared.Pinpointer;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Impstation.SpawnedFromTracker;
using Content.Shared._Polonium.Replicator;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorColossusFoundrySystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> PartTag = "ReplicatorColossusPart";

    private static readonly SpriteSpecifier InitializeIcon =
        new SpriteSpecifier.Rsi(new ResPath("/Textures/_Polonium/Interface/Actions/replicator_colossus.rsi"), "initialize");

    private const CollisionGroup SolidBlockers = CollisionGroup.Impassable | CollisionGroup.MidImpassable
                                                 | CollisionGroup.HighImpassable | CollisionGroup.LowImpassable;

    private const CollisionGroup HatchBlockers = CollisionGroup.Impassable | CollisionGroup.LowImpassable;

    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ReplicatorConstructionSystem _construction = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ReplicatorHiveSystem _hive = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    private bool _builtThisRound;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorBuildAttemptEvent>(OnBuildAttempt);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        SubscribeLocalEvent<ReplicatorColossusFoundryComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ReplicatorColossusFoundryComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ReplicatorColossusFoundryComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ReplicatorColossusFoundryComponent, ReplicatorColossusInsertPartDoAfterEvent>(OnInsertDoAfter);
        SubscribeLocalEvent<ReplicatorColossusFoundryComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ReplicatorColossusFoundryComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ReplicatorColossusFoundryComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<ReplicatorColossusFoundryComponent, LatheGetResultEvent>(OnLatheResult);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _builtThisRound = false;
    }

    private void OnBuildAttempt(ref ReplicatorBuildAttemptEvent args)
    {
        if (args.FailReason != null
            || !_prototype.Index(args.Prototype).TryComp<ReplicatorColossusFoundryComponent>(out var foundry, _factory))
            return;

        if (_builtThisRound)
        {
            args.FailReason = Loc.GetString("replicator-colossus-foundry-fail-built");
            return;
        }

        if (!_hive.TryGetHive(args.User, out var hive) || hive.Comp2.CurrentLevel < foundry.RequiredNestLevel)
        {
            args.FailReason = Loc.GetString("replicator-colossus-foundry-fail-level", ("level", foundry.RequiredNestLevel));
            return;
        }

        args.FailReason = CheckRoom(args.User, args.Location, foundry);
    }

    private string? CheckRoom(EntityUid user, EntityCoordinates location, ReplicatorColossusFoundryComponent foundry)
    {
        var floorSize = foundry.RoomRadius * 2 + 1;
        var footprintSize = foundry.FootprintRadius * 2 + 1;

        if (_transform.GetGrid(location) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return Loc.GetString("replicator-colossus-foundry-fail-floor", ("size", floorSize));

        var center = _map.TileIndicesFor(gridUid, grid, location);
        for (var x = -foundry.RoomRadius; x <= foundry.RoomRadius; x++)
        {
            for (var y = -foundry.RoomRadius; y <= foundry.RoomRadius; y++)
            {
                if (!_map.TryGetTileRef(gridUid, grid, center + new Vector2i(x, y), out var tile) || _turf.IsSpace(tile))
                    return Loc.GetString("replicator-colossus-foundry-fail-floor", ("size", floorSize));
            }
        }

        for (var x = -foundry.FootprintRadius; x <= foundry.FootprintRadius; x++)
        {
            for (var y = -foundry.FootprintRadius; y <= foundry.FootprintRadius; y++)
            {
                if (!_map.TryGetTileRef(gridUid, grid, center + new Vector2i(x, y), out var tile))
                    continue;

                if (_turf.IsTileBlocked(tile, SolidBlockers))
                    return Loc.GetString("replicator-colossus-foundry-fail-blocked", ("size", footprintSize));

                if (!_construction.IsHiveFloor(user, tile))
                    return Loc.GetString("replicator-colossus-foundry-fail-hive-floor", ("size", footprintSize));
            }
        }

        _mobs.Clear();
        _lookup.GetEntitiesInRange(location, foundry.FootprintRadius + 0.5f, _mobs);
        foreach (var mob in _mobs)
        {
            if (!_mobState.IsDead(mob, mob.Comp) && !HasComp<ReplicatorComponent>(mob))
                return Loc.GetString("replicator-colossus-foundry-fail-mob");
        }

        return null;
    }

    private void OnMapInit(Entity<ReplicatorColossusFoundryComponent> ent, ref MapInitEvent args)
    {
        _builtThisRound = true;
        UpdateAppearance(ent);
    }

    private void OnShutdown(Entity<ReplicatorColossusFoundryComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.WaitingSpawner is { } spawner)
            QueueDel(spawner);
    }

    private void OnInteractUsing(Entity<ReplicatorColossusFoundryComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, PartTag))
            return;

        args.Handled = true;

        if (!HasComp<ReplicatorComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("replicator-colossus-foundry-part-not-replicator"), ent, args.User);
            return;
        }

        if (GetInsertBlocker(ent, args.Used) is { } blocker)
        {
            _popup.PopupEntity(blocker, ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.InsertTime, new ReplicatorColossusInsertPartDoAfterEvent(),
            ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnInsertDoAfter(Entity<ReplicatorColossusFoundryComponent> ent, ref ReplicatorColossusInsertPartDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } used)
            return;

        args.Handled = true;

        if (GetInsertBlocker(ent, used) is { } blocker)
        {
            _popup.PopupEntity(blocker, ent, args.User);
            return;
        }

        QueueDel(used);
        ent.Comp.Stage++;
        UpdateAppearance(ent);

        if (!IsComplete(ent.Comp))
        {
            _audio.PlayPvs(ent.Comp.InsertSound, ent);
            return;
        }

        _audio.PlayPvs(ent.Comp.CompleteSound, ent);

        if (_hive.TryGetHive(args.User, out var hive))
            NotifyHive(hive, Loc.GetString("replicator-colossus-foundry-complete"));

        var location = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(ent.Owner));
        _chat.DispatchGlobalAnnouncement(Loc.GetString(ent.Comp.WarningAnnouncement, ("location", location)),
            Loc.GetString("comms-console-announcement-title-centcom"),
            colorOverride: Color.Red,
            centComAnnouncement: true);
    }

    private string? GetInsertBlocker(Entity<ReplicatorColossusFoundryComponent> ent, EntityUid part)
    {
        if (IsComplete(ent.Comp))
            return Loc.GetString("replicator-colossus-foundry-part-complete");

        var next = ent.Comp.Parts[ent.Comp.Stage];
        if (MetaData(part).EntityPrototype?.ID == next.Id)
            return null;

        return Loc.GetString("replicator-colossus-foundry-part-wrong", ("part", _prototype.Index(next).Name));
    }

    private void OnGetVerbs(Entity<ReplicatorColossusFoundryComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<ReplicatorComponent>(args.User))
            return;

        var user = args.User;
        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("replicator-colossus-foundry-verb-initialize"),
            Icon = InitializeIcon,
            Act = () => TryInitialize(ent, user),
        };

        if (GetInitializeBlocker(ent.Comp) is { } blocker)
        {
            verb.Disabled = true;
            verb.Message = blocker;
        }

        args.Verbs.Add(verb);
    }

    private string? GetInitializeBlocker(ReplicatorColossusFoundryComponent foundry)
    {
        if (foundry.WaitingSpawner != null)
            return Loc.GetString("replicator-colossus-foundry-awaiting");

        if (!IsComplete(foundry))
            return Loc.GetString("replicator-colossus-foundry-incomplete");

        return null;
    }

    private void TryInitialize(Entity<ReplicatorColossusFoundryComponent> ent, EntityUid user)
    {
        if (GetInitializeBlocker(ent.Comp) is { } blocker)
        {
            _popup.PopupEntity(blocker, ent, user);
            return;
        }

        if (!_hive.TryGetHive(user, out var hive))
        {
            _popup.PopupEntity(Loc.GetString("replicator-construction-fail-no-nest"), ent, user);
            return;
        }

        var spawner = Spawn(ent.Comp.Spawner, Transform(ent).Coordinates);
        EnsureComp<SpawnedFromTrackerComponent>(spawner).SpawnedFrom = hive;
        EnsureComp<ReplicatorColossusSpawnerComponent>(spawner).Foundry = ent;

        ent.Comp.Nest = hive;
        ent.Comp.WaitingSpawner = spawner;
        UpdateAppearance(ent);

        _audio.PlayPvs(ent.Comp.InitializeSound, ent);
        NotifyHive(hive, Loc.GetString("replicator-colossus-foundry-initializing"));
    }

    private void OnExamined(Entity<ReplicatorColossusFoundryComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(ReplicatorColossusFoundryComponent)))
        {
            if (ent.Comp.WaitingSpawner != null)
            {
                args.PushMarkup(Loc.GetString("replicator-colossus-foundry-examine-awaiting"));
                return;
            }

            args.PushMarkup(Loc.GetString("replicator-colossus-foundry-examine-stage",
                ("stage", ent.Comp.Stage),
                ("total", ent.Comp.Parts.Count)));

            if (!IsComplete(ent.Comp) && HasComp<ReplicatorComponent>(args.Examiner))
            {
                args.PushMarkup(Loc.GetString("replicator-colossus-foundry-examine-next",
                    ("part", _prototype.Index(ent.Comp.Parts[ent.Comp.Stage]).Name)));
            }
        }
    }

    private void OnUiOpenAttempt(Entity<ReplicatorColossusFoundryComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<ReplicatorComponent>(args.User))
            args.Cancel();
    }

    private void OnLatheResult(Entity<ReplicatorColossusFoundryComponent> ent, ref LatheGetResultEvent args)
    {
        _transform.SetCoordinates(args.ResultItem, GetHatchCoordinates(ent));
    }

    private EntityCoordinates GetHatchCoordinates(Entity<ReplicatorColossusFoundryComponent> ent)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return xform.Coordinates;

        var origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var reach = ent.Comp.FootprintRadius + 1;
        Span<Vector2i> offsets =
        [
            new(0, -reach), new(reach, 0), new(-reach, 0), new(0, reach),
            new(1, -reach), new(-1, -reach), new(reach, 1), new(reach, -1),
            new(-reach, 1), new(-reach, -1), new(1, reach), new(-1, reach),
        ];

        foreach (var offset in offsets)
        {
            var indices = origin + offset;
            if (!_map.TryGetTileRef(gridUid, grid, indices, out var tile)
                || _turf.IsSpace(tile)
                || _turf.IsTileBlocked(tile, HatchBlockers))
                continue;

            return _map.GridTileToLocal(gridUid, grid, indices);
        }

        return xform.Coordinates.Offset(new Vector2(0, -reach));
    }

    private void UpdateAppearance(Entity<ReplicatorColossusFoundryComponent> ent)
    {
        var state = ent.Comp.WaitingSpawner != null
            ? ReplicatorColossusFoundryState.Awaiting
            : IsComplete(ent.Comp)
                ? ReplicatorColossusFoundryState.Complete
                : ReplicatorColossusFoundryState.Assembling;

        _appearance.SetData(ent, ReplicatorColossusFoundryVisuals.Stage, ent.Comp.Stage);
        _appearance.SetData(ent, ReplicatorColossusFoundryVisuals.State, state);
    }

    private void NotifyHive(Entity<ReplicatorHiveComponent, ReplicatorNestComponent> hive, string message)
    {
        foreach (var minion in hive.Comp2.SpawnedMinions)
        {
            if (!TerminatingOrDeleted(minion) && _mobState.IsAlive(minion))
                _popup.PopupEntity(message, minion, minion, PopupType.Medium);
        }
    }

    private static bool IsComplete(ReplicatorColossusFoundryComponent foundry)
    {
        return foundry.Stage >= foundry.Parts.Count;
    }
}

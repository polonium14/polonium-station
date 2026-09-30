using Content.Server.Destructible;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Wall;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorConstructionSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> DiagonalTag = "Diagonal";

    private const CollisionGroup SolidBlockers = CollisionGroup.Impassable | CollisionGroup.MidImpassable
                                                 | CollisionGroup.HighImpassable | CollisionGroup.LowImpassable;

    private const float MobSearchHalfSize = 0.45f;

    private static readonly SoundSpecifier ConvertSound = new SoundPathSpecifier("/Audio/_Polonium/Effects/Replicators/convert.ogg");

    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ReplicatorHiveSystem _hive = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private TurfSystem _turf = default!;

    private readonly List<EntityUid> _anchored = new();
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorComponent, ReplicatorConvertActionEvent>(OnConvert);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorConvertDoAfterEvent>(OnConvertDoAfter);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorBuildActionEvent>(OnBuild);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorBuildDoAfterEvent>(OnBuildDoAfter);
    }

    #region Conversion

    private void OnConvert(Entity<ReplicatorComponent> ent, ref ReplicatorConvertActionEvent args)
    {
        if (args.Handled || !TryGetHive(ent, out var hive))
            return;

        if (!TryGetFloor(args.Target, out var grid, out var tile))
        {
            Fail(ent, "replicator-construction-fail-no-floor");
            return;
        }

        EntityUid? wall = null;
        EntProtoId? replacement = null;
        ProtoId<ContentTileDefinition>? floor = null;
        int cost;

        if (FindWall(grid, tile.GridIndices) is { } found)
        {
            if (!CanConvertWall(found, out var reason))
            {
                Fail(ent, reason);
                return;
            }

            wall = found;
            replacement = _destructible.DestroyedAt(found).Float() >= args.ReinforcedThreshold ? args.ReinforcedWall : args.Wall;
            cost = args.WallCost;
        }
        else
        {
            if (((ContentTileDefinition)_tileDefinitions[tile.Tile.TypeId]).ID == args.Tile)
            {
                Fail(ent, "replicator-construction-fail-already-ours");
                return;
            }

            floor = args.Tile;
            cost = args.TileCost;
        }

        if (!_hive.CanAfford(hive, cost))
        {
            Fail(ent, "replicator-construction-fail-budget", cost);
            return;
        }

        var location = _map.GridTileToLocal(grid, grid, tile.GridIndices);
        var ev = new ReplicatorConvertDoAfterEvent(GetNetCoordinates(location), GetNetEntity(wall), replacement, floor, cost, args.Effect);
        var doAfter = new DoAfterArgs(EntityManager, ent, args.Delay, ev, ent, target: wall)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private void OnConvertDoAfter(Entity<ReplicatorComponent> ent, ref ReplicatorConvertDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (!TryGetHive(ent, out var hive))
            return;

        var location = GetCoordinates(args.Location);
        if (!TryGetFloor(location, out var grid, out var tile))
            return;

        if (args.Wall is { } netWall)
        {
            var wall = GetEntity(netWall);
            if (args.Replacement is not { } replacement
                || FindWall(grid, tile.GridIndices) != wall
                || !CanConvertWall(wall, out _))
                return;

            if (!_hive.TrySpend(hive, args.Cost))
            {
                Fail(ent, "replicator-construction-fail-budget", args.Cost);
                return;
            }

            QueueDel(wall);
            Spawn(replacement, location);
        }
        else if (args.Tile is { } floor)
        {
            var tileDef = (ContentTileDefinition)_tileDefinitions[floor.Id];
            if (tile.Tile.TypeId == tileDef.TileId)
                return;

            if (!_hive.TrySpend(hive, args.Cost))
            {
                Fail(ent, "replicator-construction-fail-budget", args.Cost);
                return;
            }

            _tile.ReplaceTile(tile, tileDef);
        }

        if (args.Effect is { } effect)
            Spawn(effect, location);

        _audio.PlayPvs(ConvertSound, location, AudioParams.Default.WithVolume(-4f).WithVariation(0.1f));
    }

    private EntityUid? FindWall(Entity<MapGridComponent> grid, Vector2i indices)
    {
        _anchored.Clear();
        _map.GetAnchoredEntities(grid, indices, _anchored);

        foreach (var uid in _anchored)
        {
            if (HasComp<WallComponent>(uid))
                return uid;
        }

        return null;
    }

    private bool CanConvertWall(EntityUid wall, out string reason)
    {
        if (_tag.HasTag(wall, ReplicatorHiveSystem.StructureTag))
        {
            reason = "replicator-construction-fail-already-ours";
            return false;
        }

        if (_tag.HasTag(wall, DiagonalTag) || !HasComp<DestructibleComponent>(wall))
        {
            reason = "replicator-construction-fail-wall";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    #endregion

    #region Building

    private void OnBuild(Entity<ReplicatorComponent> ent, ref ReplicatorBuildActionEvent args)
    {
        if (args.Handled || !TryGetHive(ent, out var hive))
            return;

        if (!TryGetFloor(args.Target, out var grid, out var tile))
        {
            Fail(ent, "replicator-construction-fail-no-floor");
            return;
        }

        if (IsOccupied(grid, tile, args.Prototype, args.Solid))
        {
            Fail(ent, "replicator-construction-fail-occupied");
            return;
        }

        if (!_hive.CanAfford(hive, args.Cost))
        {
            Fail(ent, "replicator-construction-fail-budget", args.Cost);
            return;
        }

        var location = _map.GridTileToLocal(grid, grid, tile.GridIndices);
        var multiplier = TryComp<ReplicatorBuilderComponent>(ent, out var builder) ? builder.BuildTimeMultiplier : 1f;
        var delay = args.Delay * multiplier;

        if (delay <= TimeSpan.Zero)
        {
            if (_hive.TrySpend(hive, args.Cost))
                Spawn(args.Prototype, location);

            args.Handled = true;
            return;
        }

        EntityUid? effect = args.Effect is { } effectProto ? Spawn(effectProto, location) : null;
        var ev = new ReplicatorBuildDoAfterEvent(GetNetCoordinates(location), args.Prototype, args.Cost, args.Solid, GetNetEntity(effect));
        var doAfter = new DoAfterArgs(EntityManager, ent, delay, ev, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
        else if (effect != null)
            QueueDel(effect.Value);
    }

    private void OnBuildDoAfter(Entity<ReplicatorComponent> ent, ref ReplicatorBuildDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (GetEntity(args.Effect) is { } effect)
            QueueDel(effect);

        if (args.Cancelled || !TryGetHive(ent, out var hive))
            return;

        var location = GetCoordinates(args.Location);
        if (!TryGetFloor(location, out var grid, out var tile))
            return;

        if (IsOccupied(grid, tile, args.Prototype, args.Solid))
        {
            Fail(ent, "replicator-construction-fail-occupied");
            return;
        }

        if (!_hive.TrySpend(hive, args.Cost))
        {
            Fail(ent, "replicator-construction-fail-budget", args.Cost);
            return;
        }

        Spawn(args.Prototype, location);
    }

    private bool IsOccupied(Entity<MapGridComponent> grid, TileRef tile, EntProtoId prototype, bool solid)
    {
        if (solid)
        {
            if (_turf.IsTileBlocked(tile, SolidBlockers))
                return true;

            _mobs.Clear();
            var center = _map.GridTileToLocal(grid, grid, tile.GridIndices);
            _lookup.GetEntitiesInRange(center, MobSearchHalfSize, _mobs);
            return _mobs.Count > 0;
        }

        _anchored.Clear();
        _map.GetAnchoredEntities(grid, tile.GridIndices, _anchored);
        foreach (var uid in _anchored)
        {
            if (HasComp<WallComponent>(uid) || MetaData(uid).EntityPrototype?.ID == prototype.Id)
                return true;
        }

        return false;
    }

    #endregion

    private bool TryGetHive(EntityUid replicator, out Entity<ReplicatorHiveComponent, ReplicatorNestComponent> hive)
    {
        if (_hive.TryGetHive(replicator, out hive))
            return true;

        _popup.PopupEntity(Loc.GetString("replicator-construction-fail-no-nest"), replicator, replicator, PopupType.SmallCaution);
        return false;
    }

    private bool TryGetFloor(EntityCoordinates coordinates, out Entity<MapGridComponent> grid, out TileRef tile)
    {
        grid = default;
        tile = default;

        if (!_turf.TryGetTileRef(coordinates, out var tileRef)
            || _turf.IsSpace(tileRef.Value)
            || !TryComp<MapGridComponent>(tileRef.Value.GridUid, out var gridComp))
            return false;

        grid = (tileRef.Value.GridUid, gridComp);
        tile = tileRef.Value;
        return true;
    }

    private void Fail(EntityUid replicator, string message, int cost = 0)
    {
        _popup.PopupEntity(Loc.GetString(message, ("cost", ReplicatorHiveSystem.ToSheets(cost))), replicator, replicator, PopupType.SmallCaution);
    }
}

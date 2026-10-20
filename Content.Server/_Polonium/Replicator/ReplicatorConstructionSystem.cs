using Content.Server.Destructible;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
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

    private static readonly Vector2i[] SpreadOffsets = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private static readonly SoundSpecifier ConvertSound = new SoundPathSpecifier("/Audio/_Polonium/Effects/Replicators/convert.ogg");

    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ReplicatorHiveSystem _hive = default!;
    [Dependency] private ReplicatorModuleSystem _modules = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private readonly List<EntityUid> _anchored = new();
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorComponent, ReplicatorConvertFloorActionEvent>(OnConvertFloor);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorConvertFloorDoAfterEvent>(OnConvertFloorDoAfter);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorConvertWallActionEvent>(OnConvertWall);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorConvertDoAfterEvent>(OnConvertDoAfter);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorReinforceWallActionEvent>(OnReinforceWall);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorReinforceDoAfterEvent>(OnReinforceDoAfter);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorBuildActionEvent>(OnBuild);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorBuildDoAfterEvent>(OnBuildDoAfter);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorHolowallActionEvent>(OnHolowall);

        SubscribeLocalEvent<ReplicatorBuildMenuComponent, UseInHandEvent>(OnMenuUse);
        SubscribeLocalEvent<ReplicatorBuildMenuComponent, ReplicatorBuildSelectMessage>(OnMenuSelect);
        SubscribeLocalEvent<ReplicatorBuildMenuComponent, AfterInteractEvent>(OnMenuAfterInteract);
    }

    #region Conversion

    private void OnConvertFloor(Entity<ReplicatorComponent> ent, ref ReplicatorConvertFloorActionEvent args)
    {
        if (args.Handled || !TryGetHive(ent, out var hive))
            return;

        if (!TryGetFloor(Transform(ent).Coordinates, out var grid, out var tile))
        {
            Fail(ent, "replicator-construction-fail-no-floor");
            return;
        }

        var floor = (ContentTileDefinition) _tileDefinitions[args.Tile];
        if (tile.Tile.TypeId == floor.TileId)
        {
            Fail(ent, "replicator-construction-fail-already-ours");
            return;
        }

        if (!_hive.CanAfford(hive, args.Cost))
        {
            Fail(ent, "replicator-construction-fail-budget", args.Cost);
            return;
        }

        var location = _map.GridTileToLocal(grid, grid, tile.GridIndices);
        var ev = new ReplicatorConvertFloorDoAfterEvent(GetNetCoordinates(location), args.Tile, args.Cost, args.Effect);
        var doAfter = new DoAfterArgs(EntityManager, ent, args.Delay * GetBuildTimeMultiplier(ent), ev, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private void OnConvertFloorDoAfter(Entity<ReplicatorComponent> ent, ref ReplicatorConvertFloorDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (!TryGetHive(ent, out var hive))
            return;

        var floor = (ContentTileDefinition) _tileDefinitions[args.Tile];
        if (!TryGetFloor(GetCoordinates(args.Location), out var grid, out var tile) || tile.Tile.TypeId == floor.TileId)
            return;

        if (!_hive.TrySpend(hive, args.Cost))
        {
            Fail(ent, "replicator-construction-fail-budget", args.Cost);
            return;
        }

        _tile.ReplaceTile(tile, floor);
        PlayConvert(_map.GridTileToLocal(grid, grid, tile.GridIndices), args.Effect);

        if (!_modules.HasFloorSpread(ent))
            return;

        foreach (var offset in SpreadOffsets)
        {
            if (!_map.TryGetTileRef(grid, grid, tile.GridIndices + offset, out var neighbour)
                || _turf.IsSpace(neighbour)
                || neighbour.Tile.TypeId == floor.TileId
                || !_hive.TrySpend(hive, args.Cost))
                continue;

            _tile.ReplaceTile(neighbour, floor);
            if (args.Effect != null)
                Spawn(args.Effect, _map.GridTileToLocal(grid, grid, neighbour.GridIndices));
        }
    }

    private void OnConvertWall(Entity<ReplicatorComponent> ent, ref ReplicatorConvertWallActionEvent args)
    {
        if (args.Handled || !TryGetHive(ent, out var hive))
            return;

        if (!TryGetFloor(args.Target, out var grid, out var tile) || FindWall(grid, tile.GridIndices) is not { } wall)
        {
            Fail(ent, "replicator-construction-fail-no-wall");
            return;
        }

        if (!CanConvertWall(wall, out var reason))
        {
            Fail(ent, reason);
            return;
        }

        if (!_hive.CanAfford(hive, args.Cost))
        {
            Fail(ent, "replicator-construction-fail-budget", args.Cost);
            return;
        }

        var replacement = _destructible.DestroyedAt(wall).Float() >= args.ReinforcedThreshold ? args.ReinforcedWall : args.Wall;
        var location = _map.GridTileToLocal(grid, grid, tile.GridIndices);
        var ev = new ReplicatorConvertDoAfterEvent(GetNetCoordinates(location), GetNetEntity(wall), replacement, args.Cost, args.Effect);
        var doAfter = new DoAfterArgs(EntityManager, ent, args.Delay * GetBuildTimeMultiplier(ent), ev, ent, target: wall)
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
        var wall = GetEntity(args.Wall);
        if (!TryGetFloor(location, out var grid, out var tile)
            || FindWall(grid, tile.GridIndices) != wall
            || !CanConvertWall(wall, out _))
            return;

        if (!_hive.TrySpend(hive, args.Cost))
        {
            Fail(ent, "replicator-construction-fail-budget", args.Cost);
            return;
        }

        QueueDel(wall);
        Spawn(args.Replacement, location);
        PlayConvert(location, args.Effect);
    }

    private void OnReinforceWall(Entity<ReplicatorComponent> ent, ref ReplicatorReinforceWallActionEvent args)
    {
        if (args.Handled || !TryGetHive(ent, out var hive))
            return;

        if (!TryGetFloor(args.Target, out var grid, out var tile) || FindWall(grid, tile.GridIndices) is not { } wall)
        {
            Fail(ent, "replicator-construction-fail-no-wall");
            return;
        }

        if (!CanReinforceWall(wall, args.Wall, out var reason))
        {
            Fail(ent, reason);
            return;
        }

        if (!_hive.CanAfford(hive, args.Cost))
        {
            Fail(ent, "replicator-construction-fail-budget", args.Cost);
            return;
        }

        var location = _map.GridTileToLocal(grid, grid, tile.GridIndices);
        var ev = new ReplicatorReinforceDoAfterEvent(GetNetCoordinates(location), GetNetEntity(wall), args.ReinforcedWall, args.Cost, args.Effect);
        var doAfter = new DoAfterArgs(EntityManager, ent, args.Delay, ev, ent, target: wall)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private void OnReinforceDoAfter(Entity<ReplicatorComponent> ent, ref ReplicatorReinforceDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (!TryGetHive(ent, out var hive))
            return;

        var location = GetCoordinates(args.Location);
        var wall = GetEntity(args.Wall);
        if (!TryGetFloor(location, out var grid, out var tile)
            || FindWall(grid, tile.GridIndices) != wall
            || !CanReinforceWall(wall, null, out _))
            return;

        if (!_hive.TrySpend(hive, args.Cost))
        {
            Fail(ent, "replicator-construction-fail-budget", args.Cost);
            return;
        }

        QueueDel(wall);
        Spawn(args.Replacement, location);
        PlayConvert(location, args.Effect);
    }

    private bool CanReinforceWall(EntityUid wall, EntProtoId? required, out string reason)
    {
        reason = string.Empty;

        if (!_tag.HasTag(wall, ReplicatorHiveSystem.StructureTag))
        {
            reason = "replicator-construction-fail-reinforce-foreign";
            return false;
        }

        if (required != null && MetaData(wall).EntityPrototype?.ID != required.Value.Id)
        {
            reason = "replicator-construction-fail-reinforce-done";
            return false;
        }

        return true;
    }

    private void PlayConvert(EntityCoordinates location, EntProtoId? effect)
    {
        if (effect != null)
            Spawn(effect, location);

        _audio.PlayPvs(ConvertSound, location, AudioParams.Default.WithVolume(-7f).WithVariation(0.1f));
    }

    public EntityUid? FindWall(Entity<MapGridComponent> grid, Vector2i indices)
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

    public bool CanConvertWall(EntityUid wall, out string reason)
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

        if (!CanPlace(ent, args.Prototype, args.Target, out var location))
            return;

        if (!_hive.CanAfford(hive, args.Cost))
        {
            Fail(ent, "replicator-construction-fail-budget", args.Cost);
            return;
        }

        var delay = args.Delay * GetBuildTimeMultiplier(ent);
        if (delay <= TimeSpan.Zero)
        {
            if (_hive.TrySpend(hive, args.Cost))
                Spawn(args.Prototype, location);

            args.Handled = true;
            return;
        }

        var ev = new ReplicatorBuildDoAfterEvent(default, args.Prototype, args.Cost, null);
        if (StartBuild(ent, ev, location, delay, args.Effect, null))
            args.Handled = true;
    }

    private bool StartBuild(EntityUid user, ReplicatorBuildDoAfterEvent ev, EntityCoordinates location, TimeSpan delay,
        EntProtoId? effectProto, EntityUid? used)
    {
        EntityUid? effect = effectProto != null ? Spawn(effectProto, location) : null;
        ev.Location = GetNetCoordinates(location);
        ev.Effect = GetNetEntity(effect);

        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, user, used: used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = used != null,
            BreakOnDropItem = used != null,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            return true;

        if (effect != null)
            QueueDel(effect.Value);

        return false;
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

        if (!CanPlace(ent, args.Prototype, GetCoordinates(args.Location), out var location))
            return;

        if (args.Structure is not { } structureId)
        {
            if (!_hive.TrySpend(hive, args.Cost))
            {
                Fail(ent, "replicator-construction-fail-budget", args.Cost);
                return;
            }

            Spawn(args.Prototype, location);
            return;
        }

        var cost = GetCost(_prototype.Index(structureId));
        if (args.Used is not { } sheets || !_hands.IsHolding(ent.Owner, sheets) || !TryPay(hive, sheets, cost))
        {
            FailSheets(ent, cost);
            return;
        }

        var built = Spawn(args.Prototype, location);
        EnsureComp<ReplicatorHiveStructureComponent>(built).Structure = structureId;

        if (TryComp<ReplicatorBuilderComponent>(ent, out var builder) && builder.Selected == structureId)
            builder.Selected = null;
    }

    private void OnHolowall(Entity<ReplicatorComponent> ent, ref ReplicatorHolowallActionEvent args)
    {
        if (args.Handled || !TryGetHive(ent, out var hive))
            return;

        if (!_turf.TryGetTileRef(Transform(ent).Coordinates, out var tile)
            || tile.Value.Tile.IsEmpty
            || !TryComp<MapGridComponent>(tile.Value.GridUid, out var grid))
        {
            Fail(ent, "replicator-construction-fail-no-floor");
            return;
        }

        _anchored.Clear();
        _map.GetAnchoredEntities((tile.Value.GridUid, grid), tile.Value.GridIndices, _anchored);
        foreach (var uid in _anchored)
        {
            if (MetaData(uid).EntityPrototype?.ID != args.Prototype.Id)
                continue;

            Fail(ent, "replicator-construction-fail-occupied");
            return;
        }

        if (!_hive.TrySpend(hive, args.Cost))
        {
            Fail(ent, "replicator-construction-fail-budget", args.Cost);
            return;
        }

        Spawn(args.Prototype, _map.GridTileToLocal(tile.Value.GridUid, grid, tile.Value.GridIndices));
        args.Handled = true;
    }

    private void OnMenuUse(Entity<ReplicatorBuildMenuComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !HasComp<ReplicatorBuilderComponent>(args.User))
            return;

        args.Handled = true;

        if (!TryGetHive(args.User, out var hive))
            return;

        var entries = new List<ReplicatorBuildMenuEntry>();
        foreach (var structure in _prototype.EnumeratePrototypes<ReplicatorStructurePrototype>())
        {
            entries.Add(new ReplicatorBuildMenuEntry(structure.ID, GetCost(structure), hive.Comp2.CurrentLevel >= structure.MinNestLevel));
        }

        _ui.SetUiState(ent.Owner, ReplicatorBuildUiKey.Key, new ReplicatorBuildMenuState(entries));
        _ui.TryOpenUi(ent.Owner, ReplicatorBuildUiKey.Key, args.User);
    }

    private void OnMenuSelect(Entity<ReplicatorBuildMenuComponent> ent, ref ReplicatorBuildSelectMessage args)
    {
        if (!TryComp<ReplicatorBuilderComponent>(args.Actor, out var builder)
            || !_prototype.TryIndex(args.Structure, out var structure))
            return;

        builder.Selected = structure.ID;
        _ui.CloseUi(ent.Owner, ReplicatorBuildUiKey.Key, args.Actor);
        _popup.PopupEntity(Loc.GetString("replicator-build-selected", ("structure", _prototype.Index(structure.Entity).Name)),
            args.Actor, args.Actor);
    }

    private void OnMenuAfterInteract(Entity<ReplicatorBuildMenuComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled
            || !TryComp<ReplicatorBuilderComponent>(args.User, out var builder)
            || builder.Selected is not { } selected
            || !_prototype.TryIndex(selected, out var structure))
            return;

        args.Handled = true;

        if (!TryGetHive(args.User, out var hive))
            return;

        if (!_transform.InRange(Transform(args.User).Coordinates, args.ClickLocation, structure.Range))
        {
            Fail(args.User, "replicator-build-fail-range");
            return;
        }

        if (hive.Comp2.CurrentLevel < structure.MinNestLevel)
        {
            _popup.PopupEntity(Loc.GetString("replicator-build-fail-level", ("level", structure.MinNestLevel)),
                args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (!CanPlace(args.User, structure.Entity, args.ClickLocation, out var location))
            return;

        var cost = GetCost(structure);
        if (!_hive.CanAfford(hive, GetShortfall(ent.Owner, cost)))
        {
            FailSheets(args.User, cost);
            return;
        }

        var ev = new ReplicatorBuildDoAfterEvent(default, structure.Entity, cost, null, structure.ID);
        StartBuild(args.User, ev, location, structure.Delay * GetBuildTimeMultiplier(args.User), structure.Effect, ent.Owner);
    }

    public int GetCost(ReplicatorStructurePrototype structure)
    {
        if (structure.CostGrowth == 1f)
            return structure.Cost;

        var standing = 0;
        var query = EntityQueryEnumerator<ReplicatorHiveStructureComponent>();
        while (query.MoveNext(out _, out var built))
        {
            if (built.Structure?.Id == structure.ID)
                standing++;
        }

        return (int) MathF.Round(structure.Cost * MathF.Pow(structure.CostGrowth, standing));
    }

    // Whatever the held stack cannot cover comes out of the nest's stock.
    private int GetShortfall(EntityUid sheets, int cost)
    {
        return Math.Max(cost - _stack.GetCount(sheets), 0) * ReplicatorHiveSystem.UnitsPerSheet;
    }

    private bool TryPay(Entity<ReplicatorHiveComponent> hive, EntityUid sheets, int cost)
    {
        var held = Math.Min(_stack.GetCount(sheets), cost);
        return _hive.TrySpend(hive, GetShortfall(sheets, cost)) && _stack.TryUse(sheets, held);
    }

    private float GetBuildTimeMultiplier(EntityUid replicator)
    {
        var multiplier = _modules.GetBuildTimeMultiplier(replicator);
        return TryComp<ReplicatorBuilderComponent>(replicator, out var builder) ? multiplier * builder.BuildTimeMultiplier : multiplier;
    }

    private bool CanPlace(EntityUid replicator, EntProtoId prototype, EntityCoordinates target, out EntityCoordinates location)
    {
        location = default;

        if (!TryGetFloor(target, out var grid, out var tile))
        {
            Fail(replicator, "replicator-construction-fail-no-floor");
            return false;
        }

        if (!IsHiveFloor(replicator, tile))
        {
            Fail(replicator, "replicator-construction-fail-not-hive-floor");
            return false;
        }

        if (IsOccupied(grid, tile))
        {
            Fail(replicator, "replicator-construction-fail-occupied");
            return false;
        }

        location = _map.GridTileToLocal(grid, grid, tile.GridIndices);

        var attempt = new ReplicatorBuildAttemptEvent(replicator, prototype, location);
        RaiseLocalEvent(ref attempt);

        if (attempt.FailReason is not { } reason)
            return true;

        _popup.PopupEntity(reason, replicator, replicator, PopupType.SmallCaution);
        return false;
    }

    public bool IsHiveFloor(EntityUid replicator, TileRef tile)
    {
        return TryComp<ReplicatorBuilderComponent>(replicator, out var builder)
               && ((ContentTileDefinition) _tileDefinitions[tile.Tile.TypeId]).ID == builder.Floor;
    }

    private bool IsOccupied(Entity<MapGridComponent> grid, TileRef tile)
    {
        if (_turf.IsTileBlocked(tile, SolidBlockers))
            return true;

        _mobs.Clear();
        var center = _map.GridTileToLocal(grid, grid, tile.GridIndices);
        _lookup.GetEntitiesInRange(center, MobSearchHalfSize, _mobs);
        return _mobs.Count > 0;
    }

    #endregion

    private bool TryGetHive(EntityUid replicator, out Entity<ReplicatorHiveComponent, ReplicatorNestComponent> hive)
    {
        if (_hive.TryGetHive(replicator, out hive))
            return true;

        _popup.PopupEntity(Loc.GetString("replicator-construction-fail-no-nest"), replicator, replicator, PopupType.SmallCaution);
        return false;
    }

    public bool TryGetFloor(EntityCoordinates coordinates, out Entity<MapGridComponent> grid, out TileRef tile)
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

    private void FailSheets(EntityUid replicator, int sheets)
    {
        _popup.PopupEntity(Loc.GetString("replicator-build-fail-sheets", ("cost", sheets)), replicator, replicator, PopupType.SmallCaution);
    }
}

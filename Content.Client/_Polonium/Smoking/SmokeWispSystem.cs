using System.Numerics;
using Content.Client._Polonium.Atmos.AirFlow;
using Content.Client._Starfall.Particles;
using Content.Shared._Polonium.Smoking;
using Content.Shared._Starfall.Particles;
using Content.Shared.CCVar;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Nutrition.Components;
using Content.Shared.Smoking;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Smoking;

/// <summary>
/// Simulates a continuous ribbon of smoke over every lit smokable: from the mouth when worn, from the item itself when
/// held or lying around. The ribbon rises, curls in a smooth noise field, widens and fades, and the air flow carries
/// it. <see cref="SmokeWispOverlay"/> draws it. Taking a lit smokable out of the mouth puffs a cloud.
/// </summary>
public sealed partial class SmokeWispSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private AirFlowVisualsSystem _airFlow = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ParticleSystem _particles = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<ParticleEffectPrototype> ExhaleEffect = "PoloniumSmokeExhale";

    private const string MaskSlot = "mask";

    /// <summary>
    /// Seconds a point of the ribbon lives. With <see cref="RiseSpeed"/> this sets the column height.
    /// </summary>
    public const float Lifetime = 1.8f;

    /// <summary>
    /// A new point joins the ribbon after this many seconds, or sooner once the source has moved
    /// <see cref="EmitDistance"/>, so a walking smoker leaves a smooth trail instead of beads.
    /// </summary>
    private const float EmitInterval = 0.04f;

    private const float EmitDistance = 0.05f;

    /// <summary>
    /// Tiles per second the smoke rises on screen.
    /// </summary>
    private const float RiseSpeed = 0.35f;

    /// <summary>
    /// Sideways sway in tiles per second: nearly straight at the tip, curling more as the smoke ages.
    /// </summary>
    private const float SwayAtTip = 0.04f;

    private const float SwayGrowth = 0.45f;

    /// <summary>
    /// Drift speed in tiles per second of smoke caught in a flow of full strength. The air flow visuals carry their
    /// own particles at 9.5; smoke lags behind them.
    /// </summary>
    private const float WindSpeed = 1.5f;

    /// <summary>
    /// Ribbons stop gaining points past this, which only matters if the frame rate collapses.
    /// </summary>
    private const int MaxPoints = 96;

    /// <summary>
    /// Where the ember of a worn smokable sits, in screen space (X right, Y up), for each direction the mob faces.
    /// Indexed by <see cref="Direction"/> / 2: south, east, north, west. Read off the lit-equipped-MASK sprites,
    /// which put the ember in the same place for cigarettes, cigars and joints; facing north it is hidden behind
    /// the head, so this is where the sprite draws its own smoke.
    /// </summary>
    // ponytail: one set for every smokable and species; per-item offsets in SmokeOriginComponent if one looks off
    private static readonly Vector2[] MouthOffsets =
    {
        new(-0.14f, 0.2f),
        new(0.2f, 0.2f),
        new(0.11f, 0.2f),
        new(-0.2f, 0.2f),
    };

    private readonly Dictionary<EntityUid, SmokeRibbon> _ribbons = new();
    private readonly List<EntityUid> _removeBuffer = new();

    private bool _enabled;
    private float _time;

    /// <summary>
    /// Every ribbon being simulated, read by <see cref="SmokeWispOverlay"/>.
    /// </summary>
    public IReadOnlyCollection<SmokeRibbon> Ribbons => _ribbons.Values;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmokableComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<SmokableComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeNetworkEvent<SmokePuffEvent>(OnSmokePuff);

        // Quality 0 turns particles off; the ribbon follows the same setting.
        Subs.CVar(_cfg, CCVars.ParticleQuality, quality => _enabled = quality > 0, true);

        _overlay.AddOverlay(new SmokeWispOverlay(this));
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<SmokeWispOverlay>();
        _ribbons.Clear();
    }

    private void OnAppearanceChange(Entity<SmokableComponent> ent, ref AppearanceChangeEvent args)
    {
        if (!args.TryGetData<SmokableState>(SmokingVisuals.Smoking, out var state) || state != SmokableState.Lit)
        {
            // The smoke already in the air lives out its life.
            if (_ribbons.TryGetValue(ent, out var ribbon))
                ribbon.Lit = false;
            return;
        }

        if (!_ribbons.TryGetValue(ent, out var existing))
            _ribbons[ent] = existing = new SmokeRibbon();

        existing.Lit = true;
    }

    private void OnUnequipped(Entity<SmokableComponent> ent, ref GotUnequippedEvent args)
    {
        if (!_enabled
            || !_timing.IsFirstTimePredicted
            || args.Slot != MaskSlot
            || !_ribbons.TryGetValue(ent, out var ribbon)
            || !ribbon.Lit)
            return;

        _particles.CreateParticle(ExhaleEffect, MouthCoordinates(args.EquipTarget));
    }

    private void OnSmokePuff(SmokePuffEvent ev)
    {
        if (_enabled && GetEntity(ev.Smoker) is { Valid: true } smoker && !TerminatingOrDeleted(smoker))
            _particles.CreateParticle(ExhaleEffect, MouthCoordinates(smoker));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_enabled)
        {
            _ribbons.Clear();
            return;
        }

        _time += frameTime;
        var eyeRotation = _eye.CurrentEye.Rotation;
        var up = (-eyeRotation).RotateVec(Vector2.UnitY);
        var right = (-eyeRotation).RotateVec(Vector2.UnitX);

        foreach (var (uid, ribbon) in _ribbons)
        {
            if (TerminatingOrDeleted(uid))
                ribbon.Lit = false;

            var source = ribbon.Lit && TryGetSmokeOrigin(uid, out var coords)
                ? coords
                : MapCoordinates.Nullspace;

            if (source.MapId != MapId.Nullspace && source.MapId != ribbon.MapId)
            {
                ribbon.Points.Clear();
                ribbon.MapId = source.MapId;
            }

            var wind = source.MapId != MapId.Nullspace ? Wind(uid, source) : Vector2.Zero;
            Advance(ribbon, frameTime, up, right, wind);

            if (source.MapId != MapId.Nullspace)
                Emit(ribbon, source.Position, frameTime);

            ribbon.Head = source.MapId != MapId.Nullspace ? source.Position : null;

            if (!ribbon.Lit && ribbon.Points.Count == 0)
                _removeBuffer.Add(uid);
        }

        foreach (var uid in _removeBuffer)
        {
            _ribbons.Remove(uid);
        }

        _removeBuffer.Clear();
    }

    private void Advance(SmokeRibbon ribbon, float frameTime, Vector2 up, Vector2 right, Vector2 wind)
    {
        var points = ribbon.Points;
        var expired = 0;

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            point.Age += frameTime;
            if (point.Age >= Lifetime)
            {
                expired = i + 1;
                continue;
            }

            var sway = Curl(point.Position, _time) * (SwayAtTip + SwayGrowth * point.Age);
            var velocity = up * RiseSpeed + right * sway.X + up * sway.Y + wind;
            point.Position += velocity * frameTime;
            points[i] = point;
        }

        // Points are kept oldest first, so the expired ones sit at the front.
        if (expired > 0)
            points.RemoveRange(0, expired);
    }

    private void Emit(SmokeRibbon ribbon, Vector2 position, float frameTime)
    {
        ribbon.SinceEmit += frameTime;

        var points = ribbon.Points;
        var moved = points.Count == 0 || (position - points[^1].Position).LengthSquared() >= EmitDistance * EmitDistance;
        if (points.Count >= MaxPoints || (ribbon.SinceEmit < EmitInterval && !moved))
            return;

        ribbon.SinceEmit = 0f;
        points.Add(new SmokePoint { Position = position });
    }

    /// <summary>
    /// A smooth field of sideways and lengthwise wobble, the same for every ribbon at a given place and time, so
    /// neighbouring points move together and the ribbon curls instead of jittering.
    /// </summary>
    private static Vector2 Curl(Vector2 position, float time)
    {
        var x = MathF.Sin(position.Y * 5.3f + time * 1.7f)
                + 0.5f * MathF.Sin(position.Y * 11.7f - time * 2.9f + position.X * 3.1f);
        var y = 0.4f * MathF.Cos(position.X * 4.1f + position.Y * 2.3f + time * 1.3f);
        return new Vector2(x, y);
    }

    private bool TryGetSmokeOrigin(EntityUid uid, out MapCoordinates coords)
    {
        if (!_container.TryGetContainingContainer(uid, out var container))
        {
            // On the floor: smoke rises from the ember, which turns with the item.
            var ember = CompOrNull<SmokeOriginComponent>(uid)?.Offset ?? SmokeOriginComponent.CigaretteEmber;
            var (position, rotation) = _transform.GetWorldPositionRotation(uid);
            coords = new MapCoordinates(position + rotation.RotateVec(ember), _transform.GetMapId(uid));
            return true;
        }

        var holder = container.Owner;
        if (_inventory.TryGetSlotEntity(holder, MaskSlot, out var mask) && mask == uid)
        {
            coords = MouthCoordinates(holder);
            return true;
        }

        if (_hands.IsHolding(holder, uid))
        {
            coords = _transform.GetMapCoordinates(holder);
            return true;
        }

        // In a pocket or a bag: no new smoke until it is out.
        coords = MapCoordinates.Nullspace;
        return false;
    }

    private MapCoordinates MouthCoordinates(EntityUid mob)
    {
        var eyeRotation = _eye.CurrentEye.Rotation;
        var (position, rotation) = _transform.GetWorldPositionRotation(mob);
        var facing = (rotation + eyeRotation).GetCardinalDir();
        var screenOffset = MouthOffsets[(int) facing / 2];

        return new MapCoordinates(position + (-eyeRotation).RotateVec(screenOffset), _transform.GetMapId(mob));
    }

    /// <summary>
    /// World velocity of the local air flow, in tiles per second.
    /// </summary>
    private Vector2 Wind(EntityUid uid, MapCoordinates coords)
    {
        if (Transform(uid).GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return Vector2.Zero;

        var tile = _map.TileIndicesFor(gridUid, grid, coords);
        var flow = _airFlow.GetFlow(gridUid, tile);
        if (flow == Vector2.Zero)
            return Vector2.Zero;

        return _transform.GetWorldRotation(gridUid).RotateVec(flow) * WindSpeed;
    }
}

/// <summary>
/// The smoke of one smokable: points oldest first, and the live source the newest point connects to.
/// </summary>
public sealed class SmokeRibbon
{
    public readonly List<SmokePoint> Points = new();
    public MapId MapId = MapId.Nullspace;

    /// <summary>
    /// Where smoke comes from right now, or null when it is out of sight (pocket, bag) or no longer lit.
    /// </summary>
    public Vector2? Head;

    public bool Lit;
    public float SinceEmit;
}

public struct SmokePoint
{
    public Vector2 Position;
    public float Age;
}

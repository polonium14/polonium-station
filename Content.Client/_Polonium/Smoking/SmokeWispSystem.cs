using System.Numerics;
using Content.Client._Polonium.Atmos.AirFlow;
using Content.Client._Starfall.Particles;
using Content.Shared._Polonium.Smoking;
using Content.Shared._Starfall.Particles;
using Content.Shared.CCVar;
using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Nutrition.Components;
using Content.Shared.Smoking;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Polonium.Smoking;

/// <summary>
/// Simulates a continuous ribbon of smoke over every lit smokable: from the mouth when worn, from the item itself when
/// held or lying around. The ribbon rises, curls in a smooth noise field, widens and fades, and the air flow carries
/// it. <see cref="SmokeWispOverlay"/> draws it. Taking a lit smokable out of the mouth puffs a cloud.
/// </summary>
public sealed partial class SmokeWispSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _resources = default!;
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

    /// <summary>
    /// <see cref="MouthOffsets"/> for species whose smokables have a mask sprite of their own (a muzzle puts the
    /// ember further out). Clothing skips the displacement map for those sprites, and so does the ember.
    /// </summary>
    private static readonly Dictionary<string, Vector2[]> SpeciesMouthOffsets = new()
    {
        ["vox"] = new Vector2[] { new(-0.172f, 0.141f), new(0.328f, 0.172f), new(0.11f, 0.2f), new(-0.328f, 0.172f) },
        ["vulpkanin"] = new Vector2[] { new(-0.141f, 0.203f), new(0.266f, 0.234f), new(0.11f, 0.2f), new(-0.266f, 0.234f) },
    };

    /// <summary>
    /// The worn sprite state a species-specific variant is looked up for, like clothing does.
    /// </summary>
    private const string LitMaskState = "lit-equipped-MASK";

    /// <summary>
    /// Where the ember of a held smokable sits, the same way as <see cref="MouthOffsets"/>, for the left and the right
    /// hand. Read off the lit-inhand sprites of the cigarette; cigars sit within a pixel of it.
    /// </summary>
    private static readonly Vector2[] LeftHandOffsets =
    {
        new(0.266f, -0.172f),
        new(0.172f, -0.172f),
        new(-0.297f, -0.172f),
        new(-0.016f, -0.172f),
    };

    /// <inheritdoc cref="LeftHandOffsets"/>
    private static readonly Vector2[] RightHandOffsets =
    {
        new(-0.297f, -0.172f),
        new(0.016f, -0.172f),
        new(0.266f, -0.172f),
        new(-0.172f, -0.172f),
    };

    private readonly Dictionary<EntityUid, SmokeRibbon> _ribbons = new();

    /// <summary>
    /// Displacement maps read from disk once, by RSI path and state; null when the file is missing.
    /// </summary>
    private readonly Dictionary<(string Rsi, string State), Image<Rgba32>?> _displacementImages = new();

    /// <summary>
    /// Whether an RSI has a worn sprite of its own for a species, looked up once per pair.
    /// </summary>
    private readonly Dictionary<(RSI Rsi, string Species), bool> _speciesMaskStates = new();

    /// <summary>
    /// Size of a mob sprite in pixels, the key of the displacement map to use.
    /// </summary>
    private const int SpriteSize = 32;
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

        foreach (var image in _displacementImages.Values)
        {
            image?.Dispose();
        }

        _displacementImages.Clear();
        _speciesMaskStates.Clear();
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
            _ribbons[ent] = existing = new SmokeRibbon(ent);

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

        _particles.CreateParticle(ExhaleEffect, MouthCoordinates(args.EquipTarget, ent));
    }

    private void OnSmokePuff(SmokePuffEvent ev)
    {
        if (_enabled && GetEntity(ev.Smoker) is { Valid: true } smoker && !TerminatingOrDeleted(smoker))
        {
            _inventory.TryGetSlotEntity(smoker, MaskSlot, out var worn);
            _particles.CreateParticle(ExhaleEffect, MouthCoordinates(smoker, worn));
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_enabled)
        {
            // Lit ones stay tracked, so their smoke comes back once particles are on again.
            foreach (var (uid, ribbon) in _ribbons)
            {
                ribbon.Points.Clear();
                ribbon.Head = null;
                ribbon.EmberVisible = false;
                if (!ribbon.Lit || TerminatingOrDeleted(uid))
                    _removeBuffer.Add(uid);
            }

            RemoveQueued();
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

            var emberVisible = false;
            var source = ribbon.Lit && TryGetSmokeOrigin(uid, out var coords, out emberVisible)
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
            ribbon.EmberVisible = ribbon.Head != null && emberVisible;

            if (!ribbon.Lit && ribbon.Points.Count == 0)
                _removeBuffer.Add(uid);
        }

        RemoveQueued();
    }

    private void RemoveQueued()
    {
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

    /// <param name="emberVisible">Whether the ember shows at <paramref name="coords"/>: not behind the head of a
    /// smoker facing away, not in a middle hand, which has no in-hand sprite to aim at, and not in the hands of a mob
    /// that draws no held items.</param>
    private bool TryGetSmokeOrigin(EntityUid uid, out MapCoordinates coords, out bool emberVisible)
    {
        emberVisible = false;
        if (!_container.TryGetContainingContainer(uid, out var container))
        {
            // On the floor: smoke rises from the ember, which turns with the item.
            var ember = CompOrNull<SmokeOriginComponent>(uid)?.Offset ?? SmokeOriginComponent.CigaretteEmber;
            var (position, rotation) = _transform.GetWorldPositionRotation(uid);
            coords = new MapCoordinates(position + rotation.RotateVec(ember), _transform.GetMapId(uid));
            emberVisible = true;
            return true;
        }

        var holder = container.Owner;
        if (_inventory.TryGetSlotEntity(holder, MaskSlot, out var mask) && mask == uid)
        {
            coords = MouthCoordinates(holder, uid);
            emberVisible = Facing(holder) != Direction.North;
            return true;
        }

        if (_hands.IsHolding(holder, uid, out var handId))
        {
            var location = _hands.TryGetHand(holder, handId, out var hand) ? hand.Value.Location : HandLocation.Middle;
            if (location == HandLocation.Middle)
            {
                coords = _transform.GetMapCoordinates(holder);
                return true;
            }

            var hands = CompOrNull<HandsComponent>(holder);
            coords = location == HandLocation.Left
                ? OffsetCoordinates(holder, LeftHandOffsets, hands?.LeftHandDisplacement)
                : OffsetCoordinates(holder, RightHandOffsets, hands?.RightHandDisplacement);
            emberVisible = hands?.ShowInHands != false;
            return true;
        }

        // In a pocket or a bag: no new smoke until it is out.
        coords = MapCoordinates.Nullspace;
        return false;
    }

    /// <summary>
    /// How far the displacement shader moves the sprite pixel at <paramref name="screenOffset"/>, in tiles on screen.
    /// Same maths as displacement.swsl: a pixel shows the one (R - 128, -(G - 128)) pixels away, so the content moves
    /// by (-(R - 128), G - 128) with Y up.
    /// </summary>
    private Vector2 DisplacementShift(DisplacementData? displacement, Direction facing, Vector2 screenOffset)
    {
        if (displacement == null
            || !displacement.SizeMaps.TryGetValue(SpriteSize, out var layer)
            || layer.RsiPath == null
            || layer.State == null
            || DisplacementImage(layer.RsiPath, layer.State) is not { } image)
            return Vector2.Zero;

        // RSI order of the four directions: south, north, east, west. A single-direction map uses its only frame.
        var frame = facing switch
        {
            Direction.North => 1,
            Direction.East => 2,
            Direction.West => 3,
            _ => 0,
        };
        var columns = image.Width / SpriteSize;
        var frames = columns * (image.Height / SpriteSize);
        if (columns == 0 || frames == 0)
            return Vector2.Zero;

        if (frame >= frames)
            frame = 0;

        var px = Math.Clamp((int) MathF.Floor(screenOffset.X * SpriteSize + SpriteSize / 2f), 0, SpriteSize - 1);
        var py = Math.Clamp((int) MathF.Floor(SpriteSize / 2f - screenOffset.Y * SpriteSize), 0, SpriteSize - 1);
        var pixel = image.GetPixelSpan()[(frame / columns * SpriteSize + py) * image.Width + frame % columns * SpriteSize + px];
        if (pixel.A == 0)
            return Vector2.Zero;

        return new Vector2(-(pixel.R - 128), pixel.G - 128) / SpriteSize;
    }

    private Image<Rgba32>? DisplacementImage(string rsi, string state)
    {
        if (_displacementImages.TryGetValue((rsi, state), out var cached))
            return cached;

        Image<Rgba32>? image = null;
        var file = new ResPath(rsi.StartsWith('/') ? rsi : $"/Textures/{rsi}") / $"{state}.png";
        if (_resources.TryContentFileRead(file, out var stream))
        {
            using (stream)
            {
                image = Image.Load<Rgba32>(stream);
            }
        }

        _displacementImages[(rsi, state)] = image;
        return image;
    }

    /// <summary>
    /// The direction the mob's sprite faces on screen.
    /// </summary>
    private Direction Facing(EntityUid mob)
    {
        return (_transform.GetWorldRotation(mob) + _eye.CurrentEye.Rotation).GetCardinalDir();
    }

    /// <summary>
    /// Where the ember of the smokable in the mob's mouth sits. A smokable with a sprite for the mob's species uses
    /// that species' offsets and no displacement map, the way clothing draws it.
    /// </summary>
    private MapCoordinates MouthCoordinates(EntityUid mob, EntityUid? smokable)
    {
        var species = CompOrNull<InventoryComponent>(mob)?.SpeciesId;
        if (species != null
            && smokable != null
            && CompOrNull<SpriteComponent>(smokable.Value)?.BaseRSI is { } rsi
            && HasSpeciesMaskState(rsi, species))
            return OffsetCoordinates(mob, SpeciesMouthOffsets.GetValueOrDefault(species, MouthOffsets), null);

        return OffsetCoordinates(mob, MouthOffsets, MaskDisplacement(mob));
    }

    private bool HasSpeciesMaskState(RSI rsi, string species)
    {
        if (!_speciesMaskStates.TryGetValue((rsi, species), out var has))
            _speciesMaskStates[(rsi, species)] = has = rsi.TryGetState($"{LitMaskState}-{species}", out _);

        return has;
    }

    /// <summary>
    /// Where the ember shows right now, for the overlay. Asked at draw time, after the engine has smoothed the
    /// movement, so the glow stays on the sprite of a walking smoker instead of a frame behind it.
    /// </summary>
    public bool TryGetEmber(SmokeRibbon ribbon, out Vector2 position)
    {
        position = default;
        if (!ribbon.Lit
            || TerminatingOrDeleted(ribbon.Owner)
            || !TryGetSmokeOrigin(ribbon.Owner, out var coords, out var visible)
            || !visible
            || coords.MapId != ribbon.MapId)
            return false;

        position = coords.Position;
        return true;
    }

    /// <summary>
    /// The displacement map the mob's species puts on the mask slot, picked the way clothing picks it.
    /// </summary>
    private DisplacementData? MaskDisplacement(EntityUid mob)
    {
        if (!TryComp<InventoryComponent>(mob, out var inventory))
            return null;

        var displacements = CompOrNull<HumanoidProfileComponent>(mob)?.Sex switch
        {
            Sex.Male when inventory.MaleDisplacements.Count > 0 => inventory.MaleDisplacements,
            Sex.Female when inventory.FemaleDisplacements.Count > 0 => inventory.FemaleDisplacements,
            _ => inventory.Displacements,
        };

        return displacements.GetValueOrDefault(MaskSlot);
    }

    /// <summary>
    /// The mob's position moved by the screen-space offset for the direction it faces, and by whatever the
    /// species' displacement map does to that pixel (a dwarf's mask sits lower).
    /// </summary>
    private MapCoordinates OffsetCoordinates(EntityUid mob, Vector2[] offsets, DisplacementData? displacement)
    {
        var eyeRotation = _eye.CurrentEye.Rotation;
        var position = _transform.GetWorldPosition(mob);
        var facing = Facing(mob);
        var screenOffset = offsets[(int) facing / 2];
        screenOffset += DisplacementShift(displacement, facing, screenOffset);

        // A stretched species sprite (a thaven is 5% taller) stretches what it wears too.
        if (TryComp<SpriteComponent>(mob, out var sprite))
            screenOffset *= sprite.Scale;

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
public sealed class SmokeRibbon(EntityUid owner)
{
    /// <summary>
    /// The smokable this smoke comes from.
    /// </summary>
    public readonly EntityUid Owner = owner;

    public readonly List<SmokePoint> Points = new();
    public MapId MapId = MapId.Nullspace;

    /// <summary>
    /// Where smoke comes from right now, or null when it is out of sight (pocket, bag) or no longer lit.
    /// </summary>
    public Vector2? Head;

    /// <summary>
    /// Whether <see cref="Head"/> is where the ember shows, so the overlay draws its glow there.
    /// </summary>
    public bool EmberVisible;

    public bool Lit;
    public float SinceEmit;
}

public struct SmokePoint
{
    public Vector2 Position;
    public float Age;
}

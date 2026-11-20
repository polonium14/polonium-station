using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Resources;
using Content.Shared.Access.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Whitelist;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Kitchen.UI;

/// <param name="Arcing">Contents that throw lightning while the microwave malfunctions.</param>
/// <param name="Melting">Contents that melt into the mess when the microwave starts.</param>
/// <param name="Sparking">Contents that throw sparks for a moment after the microwave starts.</param>
/// <param name="Mess">What melted and burnt contents turn into.</param>
public readonly record struct MicrowaveReactions(EntityWhitelist? Arcing, EntityWhitelist? Melting, EntityWhitelist? Sparking, EntProtoId? Mess);

public sealed partial class MicrowaveChamber : Control
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const int FoodScale = 2;
    private const int PlateX = 107;
    private const int PlateY = 124;
    private const int PlateRadiusX = 82;
    private const int PlateRadiusY = 30;
    private const int SpriteLift = 12;
    private const int SpriteRest = 16;
    private const int HoverLift = 2;
    private const int EnterDrop = 10;
    private const int LeaveSlide = 46;
    private const float SpinSpeed = 0.95f;
    private const float EnterTime = 0.16f;
    private const float LeaveTime = 0.22f;
    private const float SettleRate = 9f;
    private const float MarkRadius = 0.9f;

    private const float ZapStart = 0.26f;
    private const float ZapEnd = 1.81f;
    private const float ZapInterval = 0.045f;
    private const float BoltLife = 0.12f;
    private const float BoltBright = 0.6f;
    private const float BoltBend = 3f;
    private const float BoltSparkChance = 0.5f;
    private const float BurnStart = 1.06f;
    private const float BurnTime = 0.65f;
    private const float FlameInterval = 0.015f;
    private const float MeltStart = 0.3f;
    private const float MeltTime = 0.8f;
    private const float MeltSquash = 0.66f;
    private const float MeltSpread = 0.3f;
    private const float MeltSmokeInterval = 0.05f;
    private const float WispInterval = 0.3f;
    private const int PuffSize = 12;

    private const int MaxArcsPerItem = 3;
    private const int MaxArcs = 6;
    private const int ArcSegments = 6;
    private const int ArcFlare = 3;
    private const float ArcRate = 2.6f;
    private const float ArcBend = 7f;
    private const float ArcReroll = 0.07f;
    private const float ArcBright = 0.8f;

    private const float RunSpeed = 85f;
    private const float RunReachX = 18f;
    private const float RunReachY = 5f;
    private const float HopRate = 19f;
    private const float HopHeight = 3f;

    private const float FogTime = 1.8f;
    private const float FogAlpha = 0.86f;
    private const int FogSteps = 6;
    private const float WreckSmokeInterval = 0.05f;
    private const int WreckBurst = 40;

    private static readonly UIBox2i HitBox = new(-20, -32, 20, 8);
    private static readonly UIBox2i ReactionBox = new(-13, -26, 13, 0);
    private static readonly Color Mark = Color.FromHex("#b4b8bc");
    private static readonly Color MarkLit = Color.White;
    private static readonly Color ArcGlow = Color.FromHex("#4aa8ff");
    private static readonly Color ArcFlareGlow = Color.FromHex("#9fd6ff");
    private static readonly Color ArcSpark = Color.FromHex("#8fd8ff");
    private static readonly Color BoltGlow = Color.FromHex("#48dcff");
    private static readonly Color BoltSpark = Color.FromHex("#9ff0ff");
    private static readonly Color Fog = Color.FromHex("#171513");
    private static readonly Color Scorched = Color.FromHex("#4a3a2e");
    private static readonly Color[] Smoke = [Color.FromHex("#141210"), Color.FromHex("#24201d"), Color.FromHex("#34302c")];
    private static readonly Color[] Fire = [Color.FromHex("#ffd76a"), Color.FromHex("#ff9a2e"), Color.FromHex("#e2571c")];
    private static readonly int[] SmokeSizes = [2, 3, 3, 4];
    private static readonly int[] FlameSizes = [2, 3, 4];
    private static readonly int[] WreckBurstSizes = [4, 5, 6, 7];
    private static readonly int[] WreckSmokeSizes = [4, 5, 6];

    private readonly EntityWhitelistSystem _whitelist;
    private readonly MobStateSystem _mobState;
    private readonly SpriteSystem _sprite;
    private readonly Texture _unlit;
    private Texture _lit;
    private readonly Texture _shadow;
    private readonly PixelParticles _particles = new();
    private readonly List<Item> _items = new();
    private readonly List<Item> _drawOrder = new();
    private readonly List<Item> _vanished = new();
    private readonly List<Arc> _arcs = new();
    private readonly List<Bolt> _bolts = new();
    private readonly Vector2i[] _path = new Vector2i[ArcSegments + 1];
    private EntProtoId? _messSource;
    private Texture? _messIcon;
    private string _messName = string.Empty;
    private Vector2? _mouse;
    private float _rotation = 0.7f;
    private float _cookTime;
    private float _wreckAge = -1f;
    private float _wreckTimer;
    private int _nextOrder;
    private bool _cooking;
    private bool _held;

    public MicrowaveReactions Reactions;
    public bool Lit;
    public bool Sealed;
    public bool Malfunctioning;
    public bool Interactive;
    public bool ReducedMotion;

    public EntityUid? Highlighted;

    public EntityUid? Hovered { get; private set; }

    public event Action<EntityUid>? OnItemPressed;
    public event Action<EntityUid?>? OnHoverChanged;

    public MicrowaveChamber()
    {
        IoCManager.InjectDependencies(this);

        _whitelist = _entMan.System<EntityWhitelistSystem>();
        _mobState = _entMan.System<MobStateSystem>();
        _sprite = _entMan.System<SpriteSystem>();
        _unlit = _cache.GetTexture(MicrowaveDeviceWindow.TexturePath + "chamber.png");
        _lit = _cache.GetTexture(MicrowaveDeviceWindow.TexturePath + "chamber_lit.png");
        _shadow = _cache.GetTexture(MicrowaveDeviceWindow.TexturePath + "shadow.png");

        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        AddChild(_particles);
    }

    private bool Running => _cooking && _wreckAge < 0f;

    /// <summary>
    /// Replaces the picture of the lit chamber, for a microwave whose light has another colour.
    /// </summary>
    /// <param name="texture">Null brings back the stock light.</param>
    public void SetLight(ResPath? texture)
    {
        _lit = _cache.GetTexture(texture ?? new ResPath(MicrowaveDeviceWindow.TexturePath + "chamber_lit.png"));
    }

    /// <summary>
    /// Tells the chamber whether the microwave is running. The contents react to the moment it starts.
    /// </summary>
    /// <param name="underway">The cook was already running when the window opened, so the reactions to its start are over.</param>
    public void SetCooking(bool cooking, bool underway = false)
    {
        if (cooking && !_cooking)
            _cookTime = underway ? ZapEnd : 0f;

        _cooking = cooking;
    }

    /// <summary>
    /// Whether anything on the plate melts or sparks when the microwave starts.
    /// </summary>
    public bool HasStartReaction()
    {
        foreach (var item in _items)
        {
            if (item.Leave < 0f && (item.Melts || item.Sparks))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Keeps the current contents on the plate whatever the game says, so that an instant cook can still be watched.
    /// </summary>
    public void Hold()
    {
        _held = true;
        foreach (var item in _items)
        {
            if (item.Leave < 0f)
                Liquefy(item);
        }
    }

    /// <summary>
    /// Ends <see cref="Hold"/>, so the plate follows the game again.
    /// </summary>
    public void Release()
    {
        _held = false;
    }

    /// <summary>
    /// Fills the chamber with smoke for good after the microwave broke.
    /// </summary>
    /// <param name="settled">The microwave broke before the window opened, so the smoke has already filled the chamber.</param>
    public void Wreck(bool settled)
    {
        _held = false;
        _wreckAge = settled ? FogTime : 0f;
        _bolts.Clear();
        _arcs.Clear();
        _particles.Clear();
        foreach (var item in _items)
        {
            item.ArcCount = 0;
        }

        if (settled || ReducedMotion)
            return;

        for (var i = 0; i < WreckBurst; i++)
        {
            _particles.Add(
                new Vector2(_random.NextFloat(20f, 194f), _random.NextFloat(30f, 150f)),
                new Vector2(_random.NextFloat(-30f, 30f), _random.NextFloat(-30f, 10f)),
                _random.NextFloat(1f, 2.2f),
                _random.Pick(WreckBurstSizes),
                _random.Pick(Smoke),
                gravity: -6f,
                drag: 1.2f,
                fade: true);
        }
    }

    /// <summary>
    /// Updates the plate to what the microwave holds now. While it runs, things that disappear react instead of leaving.
    /// </summary>
    public void SetContents(IReadOnlyList<EntityUid> contents)
    {
        if (_held)
            return;

        var running = Running;
        var changed = false;
        _vanished.Clear();
        foreach (var item in _items)
        {
            if (item.Leave >= 0f)
                continue;

            if (item.Uid.IsValid() ? Contains(contents, item.Uid) : running)
                continue;

            if (running && item.Uid.IsValid())
            {
                _vanished.Add(item);
                continue;
            }

            Dismiss(item);
            changed = true;
        }

        foreach (var uid in contents)
        {
            if (Find(uid) is { } known)
            {
                if (known.Leave < 0f)
                    continue;

                known.Leave = -1f;
                changed = true;
                continue;
            }

            if (IsMess(uid) && TakeFormer() is { } former)
            {
                Replace(former, uid);
                continue;
            }

            var item = new Item { Uid = uid, Order = _nextOrder++ };
            Inspect(item);
            if (item.Melts || item.Sparks)
                TakeSnapshot(item);

            _items.Add(item);
            changed = true;
        }

        foreach (var item in _vanished)
        {
            if (Liquefy(item) || _entMan.Deleted(item.Uid))
                continue;

            Dismiss(item);
            changed = true;
        }

        if (changed)
            Arrange();
    }

    /// <summary>
    /// Lists what is on the plate in the order it was put in, including things that only exist as a picture by now.
    /// </summary>
    public void CopyRows(List<MicrowaveRow> rows)
    {
        rows.Clear();
        foreach (var item in _items)
        {
            if (item.Leave >= 0f)
                continue;

            if (item.Uid.IsValid() && _entMan.TryGetComponent(item.Uid, out MetaDataComponent? meta))
                item.Name = meta.EntityName;

            rows.Add(new MicrowaveRow(item.Uid, item.Name));
        }
    }

    private void Inspect(Item item)
    {
        var uid = item.Uid;
        item.Arcs = _whitelist.IsWhitelistPass(Reactions.Arcing, uid);
        item.Melts = _whitelist.IsWhitelistPass(Reactions.Melting, uid);
        // The game leaves cards with this flag off alone, so they must not look like they react.
        item.Sparks = _whitelist.IsWhitelistPass(Reactions.Sparking, uid)
                      && !(_entMan.TryGetComponent(uid, out IdCardComponent? card) && !card.CanMicrowave);
        item.Mob = _entMan.HasComponent<MobStateComponent>(uid);
        item.Charred = IsMess(uid);
    }

    private bool IsMess(EntityUid uid)
    {
        return Reactions.Mess is { } mess
               && _entMan.TryGetComponent(uid, out MetaDataComponent? meta)
               && meta.EntityPrototype?.ID == mess.Id;
    }

    private void TakeSnapshot(Item item)
    {
        if (!_entMan.TryGetComponent(item.Uid, out SpriteComponent? source))
            return;

        var snapshot = _entMan.Spawn(null, MapCoordinates.Nullspace);
        var sprite = _entMan.AddComponent<SpriteComponent>(snapshot);
        _sprite.CopySprite((item.Uid, source), (snapshot, sprite));
        item.Snapshot = snapshot;
        item.Tint = sprite.Color;
    }

    private void DropSnapshot(Item item)
    {
        if (item.Snapshot.IsValid() && !_entMan.Deleted(item.Snapshot))
            _entMan.QueueDeleteEntity(item.Snapshot);

        item.Snapshot = EntityUid.Invalid;
    }

    /// <summary>
    /// Picks the thing that the mess appearing in the microwave stands for: one that melts, or an earlier copy of the mess.
    /// </summary>
    private Item? TakeFormer()
    {
        for (var i = 0; i < _vanished.Count; i++)
        {
            var item = _vanished[i];
            if (!item.Melts && !item.Charred)
                continue;

            _vanished.RemoveAt(i);
            return item;
        }

        return null;
    }

    /// <summary>
    /// Leaves only the picture of a thing that melts, for when the game has nothing to show in its place.
    /// </summary>
    private bool Liquefy(Item item)
    {
        if (!item.Melts || item.Stage != Stage.Whole || !item.Snapshot.IsValid())
            return false;

        StopArcs(item);
        item.Uid = EntityUid.Invalid;
        item.Arcs = false;
        item.Mob = false;
        item.Name = MessName();
        item.Stage = Stage.Melting;
        item.StageTime = 0f;
        return true;
    }

    /// <summary>
    /// The game swapped one thing for another while the door was shut, which is how a melted item becomes the mess.
    /// </summary>
    private void Replace(Item item, EntityUid uid)
    {
        if (item.Melts && item.Stage == Stage.Whole && item.Snapshot.IsValid())
        {
            item.Stage = Stage.Melting;
            item.StageTime = 0f;
        }

        item.Uid = uid;
        Inspect(item);
        StopArcs(item);
    }

    private void Dismiss(Item item)
    {
        item.Leave = 0f;
        StopArcs(item);
    }

    private void StopArcs(Item item)
    {
        if (item.ArcCount == 0)
            return;

        item.ArcCount = 0;
        _arcs.RemoveAll(arc => arc.Source == item);
    }

    private string MessName()
    {
        ResolveMess();
        return _messName;
    }

    private void ResolveMess()
    {
        if (_messSource == Reactions.Mess)
            return;

        _messSource = Reactions.Mess;
        _messIcon = null;
        _messName = string.Empty;
        if (_messSource is not { } mess || !_proto.Resolve(mess, out var prototype))
            return;

        _messIcon = _sprite.GetPrototypeIcon(prototype).Default;
        _messName = prototype.Name;
    }

    private void Arrange()
    {
        var count = 0;
        foreach (var item in _items)
        {
            if (item.Leave < 0f)
                count++;
        }

        var outer = Math.Min(count, 7);
        var inner = count - outer;
        var index = 0;
        foreach (var item in _items)
        {
            if (item.Leave >= 0f)
                continue;

            if (count == 1)
            {
                item.TargetRadius = 0f;
                item.TargetAngle = 0f;
            }
            else if (index < outer)
            {
                item.TargetRadius = count > 7 ? 0.74f : count <= 3 ? 0.5f : 0.62f;
                item.TargetAngle = index * MathF.Tau / outer;
            }
            else
            {
                item.TargetRadius = inner == 1 ? 0f : 0.3f;
                item.TargetAngle = (index - outer) * MathF.Tau / inner + 0.5f;
            }

            if (item.Enter <= 0f)
            {
                item.Angle = item.TargetAngle;
                item.Radius = item.TargetRadius;
            }

            while (item.TargetAngle - item.Angle > MathF.PI)
                item.Angle += MathF.Tau;
            while (item.Angle - item.TargetAngle > MathF.PI)
                item.Angle -= MathF.Tau;

            index++;
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var delta = args.DeltaSeconds;
        var running = Running;
        if (running)
        {
            _cookTime += delta;
            if (!ReducedMotion)
                _rotation = (_rotation + SpinSpeed * delta) % MathF.Tau;
        }

        var removed = false;
        var settle = ReducedMotion ? 1f : MathF.Min(1f, delta * SettleRate);
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            if (item.Leave >= 0f)
                item.Leave += ReducedMotion ? 1f : delta / LeaveTime;

            if (item.Leave >= 1f || item.Uid.IsValid() && _entMan.Deleted(item.Uid) && !Ignite(item))
            {
                removed |= item.Leave < 0f;
                StopArcs(item);
                DropSnapshot(item);
                _items.RemoveAt(i);
                continue;
            }

            item.Enter = ReducedMotion ? 1f : MathF.Min(1f, item.Enter + delta / EnterTime);
            item.Angle += (item.TargetAngle - item.Angle) * settle;
            item.Radius += (item.TargetRadius - item.Radius) * settle;

            var angle = _rotation + item.Angle;
            var depth = MathF.Sin(angle) * item.Radius;
            item.X = PlateX + (int) MathF.Round(PlateRadiusX * item.Radius * MathF.Cos(angle));
            item.Y = PlateY + (int) MathF.Round(PlateRadiusY * depth);
            item.Depth = depth;

            if (running && item.Leave < 0f)
            {
                React(item, delta);
                continue;
            }

            if (item.Leave < 0f && item.Stage is Stage.Melting or Stage.Burning)
                Char(item);

            item.Run += -item.Run * settle;
            item.Hop = 0;
            item.Facing = Direction.South;
        }

        if (removed)
            Arrange();

        _drawOrder.Clear();
        _drawOrder.AddRange(_items);
        _drawOrder.Sort(static (a, b) => a.Depth != b.Depth ? a.Depth.CompareTo(b.Depth) : a.Order.CompareTo(b.Order));

        UpdateHover();
        UpdateArcs(delta, running && Sealed && Malfunctioning && !ReducedMotion);
        UpdateBolts(delta);

        if (_wreckAge < 0f)
            return;

        _wreckAge += delta;
        _wreckTimer -= delta;
        if (ReducedMotion || _wreckTimer > 0f)
            return;

        _wreckTimer = WreckSmokeInterval;
        _particles.Add(
            new Vector2(_random.NextFloat(16f, 198f), _random.NextFloat(30f, 160f)),
            new Vector2(_random.NextFloat(-14f, 14f), _random.NextFloat(-18f, 4f)),
            _random.NextFloat(1.2f, 2.4f),
            _random.Pick(WreckSmokeSizes),
            _random.Pick(Smoke),
            gravity: -6f,
            drag: 0.8f,
            fade: true);
    }

    /// <summary>
    /// A thing that was deleted behind the shut door stays as a burning picture if it is one that sparks.
    /// </summary>
    private bool Ignite(Item item)
    {
        if (!Running || item.Leave >= 0f || !item.Sparks || item.Stage != Stage.Whole || !item.Snapshot.IsValid())
            return false;

        item.Uid = EntityUid.Invalid;
        item.Stage = Stage.Burning;
        item.StageTime = 0f;
        return true;
    }

    private void React(Item item, float delta)
    {
        var box = ReactionBox.Translated(new Vector2i(item.X, item.Y));
        var alive = item.Mob && _mobState.IsAlive(item.Uid);
        switch (item.Stage)
        {
            case Stage.Melting:
                if (_cookTime < MeltStart)
                    return;

                item.StageTime = ReducedMotion ? 1f : item.StageTime + delta / MeltTime;
                Scorch(item);
                if (item.StageTime >= 1f)
                    Char(item, box);
                else if (Due(item, delta, MeltSmokeInterval))
                    Puff(_random.NextFloat(box.Left, box.Right), _random.NextFloat((box.Top + box.Bottom) / 2f, box.Bottom), _random.NextFloat(0.5f, 1f));

                return;

            case Stage.Burning:
                if (_cookTime < BurnStart && !ReducedMotion)
                {
                    Zap(item, box, delta);
                    return;
                }

                item.StageTime = ReducedMotion ? 1f : item.StageTime + delta / BurnTime;
                Scorch(item);
                if (item.StageTime >= 1f)
                {
                    Char(item, box);
                }
                else if (Due(item, delta, FlameInterval))
                {
                    _particles.Add(
                        new Vector2(_random.NextFloat(box.Left + 2f, box.Right - 3f), _random.NextFloat(box.Top + 6f, box.Bottom - 2f)),
                        new Vector2(_random.NextFloat(-6f, 6f), _random.NextFloat(-70f, -34f)),
                        _random.NextFloat(0.16f, 0.38f),
                        _random.Pick(FlameSizes),
                        _random.Pick(Fire));
                }

                return;

            case Stage.Whole when item.Sparks && Sealed && _cookTime >= ZapStart && _cookTime < ZapEnd && !ReducedMotion:
                Zap(item, box, delta);
                return;
        }

        if (alive && Sealed && !ReducedMotion)
        {
            Scurry(item, delta);
            return;
        }

        item.Hop = 0;
        if (!ReducedMotion && (item.Stage == Stage.Mess || item.Charred || item.Mob && !alive) && Due(item, delta, WispInterval))
            Puff(_random.NextFloat(box.Left + 4f, box.Right - 4f) + item.Run.X, box.Top + 6f + item.Run.Y, _random.NextFloat(0.7f, 1.2f), 2);
    }

    private void Scorch(Item item)
    {
        if (_entMan.TryGetComponent(item.Snapshot, out SpriteComponent? sprite))
            _sprite.SetColor((item.Snapshot, sprite), Color.InterpolateBetween(item.Tint, item.Tint * Scorched, MathF.Min(1f, item.StageTime)));
    }

    private static bool Due(Item item, float delta, float interval)
    {
        item.Timer -= delta;
        if (item.Timer > 0f)
            return false;

        item.Timer = interval;
        return true;
    }

    private void Scurry(Item item, float delta)
    {
        item.RunTimer -= delta;
        if (item.RunTimer <= 0f)
        {
            item.RunTimer = _random.NextFloat(0.16f, 0.36f);
            item.RunTarget = new Vector2(_random.NextFloat(-RunReachX, RunReachX), _random.NextFloat(-RunReachY, RunReachY));
        }

        var path = item.RunTarget - item.Run;
        var distance = path.Length();
        if (distance > 0.5f)
        {
            item.Run += path / distance * MathF.Min(distance, RunSpeed * delta);
            if (MathF.Abs(path.X) > 1f)
                item.Facing = path.X > 0f ? Direction.East : Direction.West;
        }

        item.Hop = (int) MathF.Round(MathF.Abs(MathF.Sin(_cookTime * HopRate)) * HopHeight);
    }

    private void Char(Item item, UIBox2i box)
    {
        if (!ReducedMotion)
        {
            for (var i = 0; i < PuffSize; i++)
            {
                Puff(_random.NextFloat(box.Left, box.Right), _random.NextFloat(box.Top, box.Bottom), _random.NextFloat(0.6f, 1.2f));
            }
        }

        Char(item);
    }

    private void Char(Item item)
    {
        DropSnapshot(item);
        item.Stage = Stage.Mess;
        item.Timer = 0f;
        if (item.Uid.IsValid())
            return;

        ResolveMess();
        item.Name = _messName;
        if (_messIcon == null)
            item.Leave = 1f;
    }

    private void Puff(float x, float y, float life, int size = 0)
    {
        _particles.Add(
            new Vector2(x, y),
            new Vector2(_random.NextFloat(-5f, 5f), _random.NextFloat(-26f, -14f)),
            life,
            size > 0 ? size : _random.Pick(SmokeSizes),
            _random.Pick(Smoke),
            gravity: -6f,
            drag: 0.6f,
            fade: true);
    }

    private void Zap(Item item, UIBox2i box, float delta)
    {
        if (!Due(item, delta, ZapInterval))
            return;

        var side = _random.Next(4);
        Vector2 from = Edge(box, side);
        Vector2 to = Edge(box, (side + _random.Next(1, 4)) % 4);
        var along = to - from;
        var length = along.Length();
        var normal = length > 0f ? new Vector2(-along.Y, along.X) / length : Vector2.Zero;
        var first = from + along / 3f + normal * _random.NextFloat(-BoltBend, BoltBend);
        var second = from + along * 2f / 3f + normal * _random.NextFloat(-BoltBend, BoltBend);
        _bolts.Add(new Bolt(Pixel(from), Pixel(first), Pixel(second), Pixel(to)));

        if (!_random.Prob(BoltSparkChance))
            return;

        _particles.Add(
            to,
            new Vector2(_random.NextFloat(-18f, 18f), _random.NextFloat(-20f, 4f)),
            _random.NextFloat(0.2f, 0.4f),
            1,
            _random.Prob(0.5f) ? Color.White : BoltSpark,
            gravity: 120f);
    }

    private Vector2i Edge(UIBox2i box, int side)
    {
        return side switch
        {
            0 => new Vector2i(_random.Next(box.Left, box.Right + 1), box.Top - 2),
            1 => new Vector2i(_random.Next(box.Left, box.Right + 1), box.Bottom + 1),
            2 => new Vector2i(box.Left - 2, _random.Next(box.Top, box.Bottom + 1)),
            _ => new Vector2i(box.Right + 1, _random.Next(box.Top, box.Bottom + 1)),
        };
    }

    private static Vector2i Pixel(Vector2 point)
    {
        return new Vector2i((int) MathF.Round(point.X), (int) MathF.Round(point.Y));
    }

    private void UpdateArcs(float delta, bool arcing)
    {
        for (var i = _arcs.Count - 1; i >= 0; i--)
        {
            var arc = _arcs[i];
            arc.Age += delta;
            if (!arcing || arc.Age >= arc.Life)
            {
                arc.Source.ArcCount--;
                _arcs.RemoveAt(i);
                continue;
            }

            arc.Reroll -= delta;
            if (arc.Reroll > 0f)
                continue;

            arc.Reroll = ArcReroll;
            Bend(arc);
        }

        if (!arcing)
            return;

        foreach (var item in _items)
        {
            if (!item.Arcs || item.Leave >= 0f || item.Stage != Stage.Whole)
                continue;

            if (item.ArcCount == 0 || item.ArcCount < MaxArcsPerItem && _arcs.Count < MaxArcs && _random.Prob(MathF.Min(1f, delta * ArcRate)))
                Strike(item);
        }
    }

    private void Strike(Item item)
    {
        var to = _random.Next(4) switch
        {
            0 => new Vector2i(_random.Next(34, 181), _random.Next(3, 16)),
            1 => new Vector2i(_random.Next(4, 27), _random.Next(24, 105)),
            2 => new Vector2i(_random.Next(188, 211), _random.Next(24, 105)),
            _ => new Vector2i(_random.Next(40, 175), _random.Next(22, 67)),
        };

        var arc = new Arc
        {
            Source = item,
            To = to,
            From = new Vector2i(_random.Next(-3, 4), _random.Next(-32, -23)),
            Life = _random.NextFloat(0.3f, 0.75f),
            Reroll = ArcReroll,
        };
        Bend(arc);
        _arcs.Add(arc);
        item.ArcCount++;

        for (var i = 0; i < 3; i++)
        {
            _particles.Add(
                to,
                new Vector2(_random.NextFloat(-26f, 26f), _random.NextFloat(-8f, 22f)),
                _random.NextFloat(0.18f, 0.4f),
                1,
                _random.Prob(0.5f) ? Color.White : ArcSpark,
                gravity: 110f);
        }
    }

    private void Bend(Arc arc)
    {
        for (var i = 1; i < ArcSegments; i++)
        {
            arc.Bends[i] = _random.NextFloat(-ArcBend, ArcBend);
        }
    }

    private void UpdateBolts(float delta)
    {
        for (var i = _bolts.Count - 1; i >= 0; i--)
        {
            var bolt = _bolts[i];
            bolt.Age += delta;
            if (bolt.Age >= BoltLife)
                _bolts.RemoveAt(i);
            else
                _bolts[i] = bolt;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        var wrecked = _wreckAge >= 0f;
        canvas.Texture(Lit ? _lit : _unlit, 0, 0);

        for (var i = 0; i < 3 && !wrecked; i++)
        {
            var angle = _rotation + i * MathF.Tau / 3f + 0.4f;
            var x = PlateX + (int) MathF.Round(PlateRadiusX * MarkRadius * MathF.Cos(angle));
            var y = PlateY + (int) MathF.Round(PlateRadiusY * MarkRadius * MathF.Sin(angle));
            canvas.Rect(x - 1, y, 3, 1, Lit ? MarkLit : Mark);
        }

        foreach (var item in _drawOrder)
        {
            DrawItem(canvas, item);
        }

        if (wrecked)
        {
            var spread = ReducedMotion ? 1f : MathF.Min(1f, _wreckAge / FogTime);
            var level = MathF.Round(spread * FogSteps) / FogSteps;
            canvas.Rect(0, 0, _unlit.Width, _unlit.Height, Fog.WithAlpha(level * FogAlpha));
        }

        foreach (var arc in _arcs)
        {
            var from = new Vector2(arc.Source.X + arc.From.X, arc.Source.Y + arc.From.Y);
            var along = arc.To - from;
            var length = along.Length();
            var normal = length > 0f ? new Vector2(-along.Y, along.X) / length : Vector2.Zero;
            for (var i = 0; i <= ArcSegments; i++)
            {
                _path[i] = Pixel(from + along * i / ArcSegments + normal * arc.Bends[i]);
            }

            var bright = arc.Age / arc.Life < ArcBright;
            Stroke(canvas, ArcSegments + 1, ArcGlow, bright);
            if (!bright)
                continue;

            Flare(canvas, _path[0]);
            Flare(canvas, _path[ArcSegments]);
        }

        foreach (var bolt in _bolts)
        {
            _path[0] = bolt.Start;
            _path[1] = bolt.First;
            _path[2] = bolt.Second;
            _path[3] = bolt.End;
            Stroke(canvas, 4, BoltGlow, bolt.Age / BoltLife < BoltBright);
        }
    }

    private void DrawItem(PixelCanvas canvas, Item item)
    {
        var x = item.X + (int) MathF.Round(item.Run.X);
        var ground = item.Y + (int) MathF.Round(item.Run.Y);
        var y = ground;
        if (item.Leave >= 0f)
        {
            y += (int) (item.Leave * item.Leave * LeaveSlide);
        }
        else
        {
            canvas.Texture(_shadow, x - _shadow.Width / 2, ground - _shadow.Height / 2 + 1);
            var falling = 1f - item.Enter;
            y -= (int) (falling * falling * EnterDrop);
            if (Interactive && item.Uid.IsValid() && (item.Uid == Hovered || item.Uid == Highlighted))
                y -= HoverLift;
        }

        y -= SpriteLift + item.Hop;
        var picture = item.Stage is Stage.Melting or Stage.Burning ? item.Snapshot : item.Uid;
        if (!picture.IsValid())
        {
            if (_messIcon is not { } icon)
                return;

            var width = icon.Width * FoodScale;
            var height = icon.Height * FoodScale;
            canvas.Stretch(icon, UIBox2.FromDimensions(0f, 0f, icon.Width, icon.Height), x - width / 2, y - height / 2, width, height);
            return;
        }

        if (!_entMan.TryGetComponent(picture, out SpriteComponent? sprite))
            return;

        if (item.Stage != Stage.Melting)
        {
            canvas.Entity(picture, sprite, x, y, new Vector2(FoodScale), item.Facing);
            return;
        }

        var tall = 1f - MeltSquash * item.StageTime;
        var wide = 1f + MeltSpread * item.StageTime;
        var rest = y + SpriteRest;
        canvas.Entity(picture, sprite, x, rest - (int) MathF.Round(SpriteRest * tall), new Vector2(FoodScale * wide, FoodScale * tall));
    }

    private void Stroke(PixelCanvas canvas, int count, Color glow, bool bright)
    {
        for (var i = 1; i < count; i++)
        {
            canvas.Line(_path[i - 1].X, _path[i - 1].Y, _path[i].X, _path[i].Y, glow, bright);
        }

        if (!bright)
            return;

        for (var i = 1; i < count; i++)
        {
            canvas.Line(_path[i - 1].X, _path[i - 1].Y, _path[i].X, _path[i].Y, Color.White);
        }
    }

    private static void Flare(PixelCanvas canvas, Vector2i point)
    {
        canvas.Rect(point.X - ArcFlare, point.Y, ArcFlare * 2 + 1, 1, ArcFlareGlow);
        canvas.Rect(point.X, point.Y - ArcFlare, 1, ArcFlare * 2 + 1, ArcFlareGlow);
        canvas.Rect(point.X - 1, point.Y - 1, 3, 3, Color.White);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        foreach (var item in _items)
        {
            DropSnapshot(item);
        }
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        _mouse = args.RelativePixelPosition;
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        _mouse = null;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || !Interactive)
            return;

        _mouse = args.RelativePixelPosition;
        UpdateHover();
        if (Hovered is not { } uid)
            return;

        OnItemPressed?.Invoke(uid);
        args.Handle();
    }

    private void UpdateHover()
    {
        EntityUid? hovered = null;
        if (Interactive && _mouse is { } mouse)
        {
            var scale = PixelCanvas.GetScale(UIScale);
            var point = new Vector2i((int) (mouse.X / scale), (int) (mouse.Y / scale));
            for (var i = _drawOrder.Count - 1; i >= 0; i--)
            {
                var item = _drawOrder[i];
                if (item.Leave >= 0f || !item.Uid.IsValid() || !HitBox.Translated(new Vector2i(item.X, item.Y)).Contains(point))
                    continue;

                hovered = item.Uid;
                break;
            }
        }

        DefaultCursorShape = hovered == null ? CursorShape.Arrow : CursorShape.Hand;
        if (hovered == Hovered)
            return;

        Hovered = hovered;
        OnHoverChanged?.Invoke(hovered);
    }

    private Item? Find(EntityUid uid)
    {
        foreach (var item in _items)
        {
            if (item.Uid == uid)
                return item;
        }

        return null;
    }

    private static bool Contains(IReadOnlyList<EntityUid> list, EntityUid uid)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == uid)
                return true;
        }

        return false;
    }

    private enum Stage : byte
    {
        Whole,
        Melting,
        Burning,
        Mess,
    }

    private sealed class Item
    {
        public EntityUid Uid;
        public EntityUid Snapshot;
        public Color Tint = Color.White;
        public string Name = string.Empty;
        public int Order;
        public float Angle;
        public float TargetAngle;
        public float Radius;
        public float TargetRadius;
        public float Enter;
        public float Leave = -1f;
        public float Depth;
        public int X;
        public int Y;
        public bool Arcs;
        public bool Melts;
        public bool Sparks;
        public bool Mob;
        public bool Charred;
        public Stage Stage;
        public float StageTime;
        public float Timer;
        public int ArcCount;
        public Vector2 Run;
        public Vector2 RunTarget;
        public float RunTimer;
        public int Hop;
        public Direction Facing = Direction.South;
    }

    private sealed class Arc
    {
        public readonly float[] Bends = new float[ArcSegments + 1];
        public Item Source = default!;
        public Vector2i From;
        public Vector2i To;
        public float Age;
        public float Life;
        public float Reroll;
    }

    private struct Bolt(Vector2i start, Vector2i first, Vector2i second, Vector2i end)
    {
        public readonly Vector2i Start = start;
        public readonly Vector2i First = first;
        public readonly Vector2i Second = second;
        public readonly Vector2i End = end;
        public float Age;
    }
}

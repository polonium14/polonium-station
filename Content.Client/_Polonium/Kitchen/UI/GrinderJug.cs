using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Resources;
using Content.Shared.Kitchen;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Random;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Polonium.Kitchen.UI;

/// <param name="Pour">How much of the mass has left the jug, from 0 to 1.</param>
/// <param name="Color">Colour of the mass, each channel from 0 to 255.</param>
public readonly record struct GrinderStream(float Pour, GrinderMassKind Kind, Vector3 Color);

/// <summary>
/// The glass jug of the blender: the things inside, the blades, the mass they turn into and the lid.
/// </summary>
public sealed partial class GrinderJug : Control
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IRobustRandom _random = default!;

    public static readonly UIBox2i Area = UIBox2i.FromDimensions(24, 20, 184, 140);

    private const int Left = GrinderArt.JugLeft;
    private const int Top = GrinderArt.JugTop;
    private const int GlassWidth = GrinderArt.JugWidth;
    private const int Floor = GrinderArt.JugFloor;
    private const int Center = GrinderArt.JugCenter;
    private const int Scale = GrinderArt.SpriteScale;

    // Things that leave slip out through the open top, so the layer they are drawn on reaches above the rim.
    private const int Headroom = 16;
    private const int LidLift = 4;
    private const int HoverLift = 2;
    private const int LevelHeight = 17;
    private const int DropIn = 30;
    private const int LeaveRise = 70;
    private const int MaxSpecks = 80;
    private const float LidTime = 0.2f;
    private const float LeaveTime = 0.25f;
    private const float HoldTime = 2f;
    private const float MaxDelta = 0.05f;
    private const float PourStart = 0.8f;
    private const float PourSteps = 10f;
    private const float ShiftSpeed = 46f;
    private const float SprayRate = 120f;
    private const float CrumbRate = 33f;
    private const float HazeRate = 18f;
    private const float BubbleRate = 18f;
    private const float DropRate = 4.8f;
    private const float Gravity = 300f;
    private const float ShakeRate = 30f;

    private static readonly Vector2i JugAt = new(26, 45);
    private static readonly Vector2i GlassAt = new(38, 50);
    private static readonly Vector2i LidAt = new(30, 26);
    private static readonly Vector2i BladesAt = new(74, 144);
    private static readonly Vector2i HubAt = new(98, 144);
    private static readonly UIBox2i Handle = new(170, 58, 206, 138);
    private static readonly (int X, int Level)[] RestSpots = [(38, 0), (70, 0), (102, 0), (54, 1), (86, 1), (70, 2)];
    private static readonly (int X, int Level)[] PairSpots = [(48, 0), (92, 0)];
    private static readonly Color BladeBlur = Color.FromHex("#d6d6d6");
    private static readonly Vector3 White = new(255f);
    private static readonly Vector3 Gray = new(128f);
    private static readonly (int From, int To, int Inset)[] Bands = MakeBands();

    private readonly Texture _jug;
    private readonly Texture _glass;
    private readonly Texture _lidTexture;
    private readonly Texture _blades;
    private readonly Texture _hub;
    private readonly GrinderMassPainter _painter = new();
    private readonly List<Item> _items = new();
    private readonly List<Item> _sorted = new();
    private readonly List<Item> _drawOrder = new();
    private readonly List<Item> _gone = new();
    private readonly List<GrinderPlanEntry> _plan = new();
    private readonly List<GrinderPlanEntry> _replanned = new();
    private readonly List<GrinderGrain> _grains = new();
    private readonly List<Particle> _particles = new();
    private readonly List<Speck> _specks = new();
    private readonly List<Drop> _drops = new();
    private readonly Action _drawItems;
    private readonly GrinderSpriteProbe _probe;
    private OwnedTexture? _massTexture;
    private IRenderTexture? _layer;
    private DrawingHandleScreen? _handle;
    private Run? _run;
    private Vector2? _mouse;
    private Vector3 _massColor = Gray;
    private GrinderMassKind _massKind;
    private float _massVolume;
    private float _lid = 1f;
    private float _clock;
    private float _dustShare;
    private int _massHeight;
    private int _layerScale;
    private bool _interactive;
    private bool _reduced;
    private bool _opened;

    public EntityUid? Highlighted;

    public EntityUid? Hovered { get; private set; }

    public event Action<EntityUid>? OnItemPressed;
    public event Action<EntityUid?>? OnHoverChanged;

    public GrinderJug(GrinderSpriteProbe probe)
    {
        IoCManager.InjectDependencies(this);

        _probe = probe;
        _jug = _cache.GetTexture(GrinderArt.TexturePath + "jug.png");
        _glass = _cache.GetTexture(GrinderArt.TexturePath + "glass.png");
        _lidTexture = _cache.GetTexture(GrinderArt.TexturePath + "lid.png");
        _blades = _cache.GetTexture(GrinderArt.TexturePath + "blades.png");
        _hub = _cache.GetTexture(GrinderArt.TexturePath + "hub.png");
        _drawItems = DrawItems;

        MouseFilter = MouseFilterMode.Pass;
    }

    /// <summary>
    /// The mass on its way out of the jug, for the spout under it.
    /// </summary>
    public GrinderStream Stream => new(_run is { Total: > 0f } run ? run.Pour : 0f, _massKind, _massColor);

    public GrinderMassPainter Painter => _painter;

    private static (int, int, int)[] MakeBands()
    {
        var bands = new List<(int, int, int)> { (-Headroom, 0, 0) };
        var start = 0;
        for (var row = 1; row <= Floor; row++)
        {
            if (row < Floor && GrinderArt.Inset(row) == GrinderArt.Inset(start))
                continue;

            bands.Add((start, row, GrinderArt.Inset(start)));
            start = row;
        }

        return bands.ToArray();
    }

    /// <summary>
    /// Brings the jug up to date with the grinder and moves everything in it on by one frame.
    /// </summary>
    public void Advance(GrinderDeviceModel model, float delta, bool reduced)
    {
        delta = MathF.Min(delta, MaxDelta);
        _reduced = reduced;
        _clock += delta;
        _dustShare = model.DustShare;

        var active = model.Program != null;
        var target = active ? 0f : 1f;
        var lidStep = delta / LidTime;
        _lid = reduced || MathF.Abs(target - _lid) <= lidStep ? target : _lid + MathF.Sign(target - _lid) * lidStep;

        var grew = SyncItems(model.Contents);
        if (_run != null && _run.Program != model.Program)
            Finish();

        if (model.Program is { } program && (_run == null || grew))
            Plan(model, program);

        Arrange();
        if (_run != null)
            StepRun(model.Progress, delta);

        _massHeight = MassHeight();
        StepItems(delta);
        StepLoose(delta);
        PaintMass();

        _interactive = !model.Busy;
        _opened = true;
        UpdateHover();
    }

    /// <returns>Whether something new came into the chamber.</returns>
    private bool SyncItems(IReadOnlyList<EntityUid> contents)
    {
        var grew = false;
        _sorted.Clear();
        for (var i = 0; i < contents.Count; i++)
        {
            var uid = contents[i];
            var item = Find(uid) ?? Recall(uid);
            if (item == null)
            {
                item = new Item { Uid = uid, Info = _probe.Get(uid), Fresh = true };
                grew = true;
            }

            _sorted.Add(item);
        }

        foreach (var item in _items)
        {
            if (_sorted.Contains(item) || _entMan.Deleted(item.Uid))
                continue;

            // Nothing can be taken out during a run, so what disappears then was ground up.
            if (_run != null || _reduced)
                continue;

            item.Leave = 0f;
            _gone.Add(item);
        }

        _items.Clear();
        _items.AddRange(_sorted);
        return grew;
    }

    /// <summary>
    /// Takes back a thing that was on its way out, when the server did not let it go after all.
    /// </summary>
    private Item? Recall(EntityUid uid)
    {
        for (var i = 0; i < _gone.Count; i++)
        {
            var item = _gone[i];
            if (item.Uid != uid)
                continue;

            _gone.RemoveAt(i);
            item.Leave = -1f;
            return item;
        }

        return null;
    }

    private void Arrange()
    {
        var count = 0;
        foreach (var item in _items)
        {
            if (item.Mode != Mode.Done)
                count++;
        }

        var index = 0;
        foreach (var item in _items)
        {
            if (item.Mode == Mode.Done)
                continue;

            var (x, level) = count switch
            {
                1 => (Center, 0),
                2 => PairSpots[index],
                _ => RestSpots[Math.Min(index, RestSpots.Length - 1)],
            };

            index++;
            item.TargetX = x;
            item.TargetY = Floor - level * LevelHeight - (item.Info.Bottom + 1 - item.Info.CenterY) * Scale;
            if (!item.Fresh)
                continue;

            item.Fresh = false;
            item.X = item.TargetX;
            item.Y = _opened && !_reduced ? -DropIn : item.TargetY;
            if (_run != null)
                Toss(item);
        }
    }

    private void Toss(Item item)
    {
        item.Mode = Mode.Tumble;
        item.Sink = 0f;
        item.VelocityX = _random.NextFloat(-30f, 30f);
        item.VelocityY = -40f - _random.NextFloat(60f);
    }

    /// <summary>
    /// Works out what the run takes from the chamber and when the blades get to each thing.
    /// It is done again when something is dropped in during the run, which the game allows.
    /// </summary>
    private void Plan(GrinderDeviceModel model, GrinderProgram program)
    {
        var starting = _run == null;
        _run ??= new Run { Program = program };
        var run = _run;

        model.BuildPlan(program, _replanned);
        var known = 0;
        foreach (var entry in _replanned)
        {
            entry.From = -1f;
            foreach (var old in _plan)
            {
                if (old.Item != entry.Item)
                    continue;

                entry.From = old.From;
                entry.To = old.To;
                entry.Spray = old.Spray;
                entry.Settled = old.Settled;
                known++;
                break;
            }
        }

        var late = Math.Max(model.Progress, 0.09f) + 0.02f;
        var count = _replanned.Count - known;
        var span = MathF.Min(0.17f, 0.5f / Math.Max(count, 1) + 0.05f);
        var index = 0;
        foreach (var entry in _replanned)
        {
            if (entry.From >= 0f)
                continue;

            if (starting)
            {
                entry.From = 0.09f + (count == 1 ? 0.12f : index * (0.52f - span) / (count - 1));
                entry.To = entry.From + span;
            }
            else
            {
                entry.From = MathF.Min(late, PourStart);
                entry.To = MathF.Min(entry.From + 0.15f, PourStart);
            }

            index++;
        }

        _plan.Clear();
        _plan.AddRange(_replanned);
        _replanned.Clear();

        run.Total = 0f;
        var dry = 0f;
        foreach (var entry in _plan)
        {
            run.Total += entry.Volume;
            dry += entry.DryVolume;
        }

        _massKind = program == GrinderProgram.Juice
            ? GrinderMassKind.Juice
            : run.Total > 0f && dry >= run.Total * _dustShare
                ? GrinderMassKind.Powder
                : GrinderMassKind.Paste;

        if (!starting)
            return;

        _massVolume = 0f;
        _massColor = _plan.Count > 0 ? GrinderArt.Rgb(_plan[0].Color) : Gray;
        _specks.Clear();
        _drops.Clear();
        foreach (var item in _items)
        {
            // What the last run took stays listed until the server deletes it, and it stays out of sight.
            if (!model.Awaited(item.Uid))
                Toss(item);
        }
    }

    private void Finish()
    {
        _run = null;
        _plan.Clear();
        _massVolume = 0f;
        _specks.Clear();
        _drops.Clear();
        foreach (var item in _items)
        {
            item.Turns = 0;
            item.Sink = 0f;

            // What the blades took stays out of sight: the server deletes it a moment after the run ends.
            if (item.Mode == Mode.Done)
                item.Hold = HoldTime;
            else
                item.Mode = Mode.Rest;
        }
    }

    private void StepRun(float progress, float delta)
    {
        var run = _run!;
        run.Shift += delta * ShiftSpeed;

        var volume = 0f;
        var mixed = Vector3.Zero;
        _grains.Clear();
        foreach (var entry in _plan)
        {
            var length = entry.To - entry.From;
            var done = length > 0f ? Math.Clamp((progress - entry.From) / length, 0f, 1f) : progress >= entry.From ? 1f : 0f;
            if (done > 0f)
            {
                volume += entry.Volume * done;
                mixed += GrinderArt.Rgb(entry.Color) * entry.Volume * done;
                foreach (var grain in entry.Grains)
                {
                    _grains.Add(grain with { Amount = grain.Amount * done });
                }
            }

            if (Find(entry.Item) is not { } item)
                continue;

            var info = item.Info;
            if (done > 0f && done < 1f)
            {
                item.Mode = Mode.Shred;
                var depth = entry.All ? info.Height + 6f : info.Height * 0.45f;
                item.Sink = entry.All ? done * depth : MathF.Sin(done * MathF.PI) * depth;
                var ease = MathF.Min(1f, delta * 12f);
                item.X += (Center - item.X) * ease;
                item.Y += (Floor - info.Height / 2f - item.Y) * ease;
                if (_reduced)
                    continue;

                entry.Spray += delta * SprayRate;
                for (; entry.Spray >= 1f; entry.Spray -= 1f)
                {
                    Spray(info, entry);
                }
            }
            else if (done >= 1f && !entry.Settled)
            {
                entry.Settled = true;
                item.Sink = 0f;
                if (entry.All)
                {
                    item.Mode = Mode.Done;
                    continue;
                }

                item.Mode = Mode.Tumble;
                item.VelocityY = -90f;
                item.VelocityX = _random.NextFloat(-40f, 40f);
            }
        }

        _massVolume = volume;
        if (volume > 0f)
            _massColor = mixed / volume;

        _painter.SetMaterial(GrinderArt.ToColor(_massColor), _grains);

        var pour = run.Total > 0f ? MathF.Max(0f, (progress - PourStart) / (1f - PourStart)) : 0f;
        run.Pour = pour >= 1f ? 1f : MathF.Max(run.Pour, MathF.Floor(pour * PourSteps) / PourSteps);

        var juice = _massKind == GrinderMassKind.Juice;
        for (var i = _specks.Count - 1; i >= 0; i--)
        {
            var speck = _specks[i];
            speck.Angle += _reduced ? 0f : delta * 4.2f;
            speck.Fade = juice ? MathF.Min(0.5f, speck.Fade + delta * 0.25f) : MathF.Min(1f, speck.Fade + delta * 0.42f);
            if (speck.Fade >= 1f)
                _specks.RemoveAt(i);
            else
                _specks[i] = speck;
        }

        var height = MassHeight();
        if (_reduced)
            return;

        if (_massKind == GrinderMassKind.Powder && height > 4)
        {
            if (_random.Prob(MathF.Min(1f, delta * CrumbRate)))
            {
                _particles.Add(new Particle
                {
                    Position = new Vector2(Center + _random.NextFloat(-40f, 40f), Floor - height * 0.7f),
                    Velocity = new Vector2(_random.NextFloat(-65f, 65f), -70f - _random.NextFloat(110f)),
                    Life = 0.9f,
                    Size = _random.Prob(0.3f) ? 2 : 1,
                    Color = _painter.CrumbColor(_random.NextDouble()),
                    Gravity = 420f,
                    Lands = true,
                });
            }

            if (_random.Prob(MathF.Min(1f, delta * HazeRate)))
            {
                _particles.Add(new Particle
                {
                    Position = new Vector2(20f + _random.NextFloat(100f), Floor - height - _random.NextFloat(8f)),
                    Velocity = new Vector2(_random.NextFloat(-10f, 10f), -8f - _random.NextFloat(14f)),
                    Life = 0.7f + _random.NextFloat(0.6f),
                    Size = 1,
                    Color = Vector3.Lerp(_massColor, White, 0.45f),
                    Fades = true,
                });
            }
        }

        if (juice && height > 6 && _random.Prob(MathF.Min(1f, delta * BubbleRate)))
        {
            _particles.Add(new Particle
            {
                Position = new Vector2(20f + _random.NextFloat(100f), Floor - 2f),
                Velocity = new Vector2(0f, -22f - _random.NextFloat(18f)),
                Life = height / 34f,
                Size = 1,
                Color = Vector3.Lerp(_massColor, White, 0.55f),
            });
        }

        if (progress > 0.12f && progress < 0.75f && height > 4 && _random.Prob(MathF.Min(1f, delta * DropRate)))
        {
            _drops.Add(new Drop
            {
                Position = new Vector2(12f + _random.NextFloat(112f), 14f + _random.NextFloat(70f - height * 0.5f)),
                Color = _massColor,
            });
        }
    }

    /// <summary>
    /// Throws up a scrap of the thing the blades are working on, in one of the colours of its picture.
    /// </summary>
    private void Spray(GrinderSpriteInfo info, GrinderPlanEntry entry)
    {
        var color = info.Palette.Length > 0 ? GrinderArt.Rgb(_random.Pick(info.Palette)) : GrinderArt.Rgb(entry.Color);
        if (_massKind == GrinderMassKind.Juice)
            color = Vector3.Lerp(color, GrinderArt.Rgb(entry.Color), 0.45f);

        _particles.Add(new Particle
        {
            Position = new Vector2(Center + _random.NextFloat(-15f, 15f), Floor - 5f),
            Velocity = new Vector2(_random.NextFloat(-80f, 80f), -50f - _random.NextFloat(130f)),
            Life = 0.45f + _random.NextFloat(0.5f),
            Size = _random.Prob(0.4f) ? 3 : 2,
            Color = color,
            Gravity = 320f,
            Lands = true,
            Scrap = true,
        });
    }

    private int MassHeight()
    {
        if (_massVolume <= 0f)
            return 0;

        var height = 66f * (1f - MathF.Exp(-_massVolume / 34f));
        if (_run != null)
            height *= 1f - _run.Pour;

        return GrinderArt.Round(height / 2f) * 2;
    }

    private void StepItems(float delta)
    {
        var floor = Floor - (_run != null ? MathF.Min(_massHeight, 40f) * 0.5f : 0f);
        foreach (var item in _items)
        {
            if (item.Mode == Mode.Rest || _reduced && item.Mode == Mode.Tumble)
            {
                var ease = MathF.Min(1f, delta * 11f);
                item.X += (item.TargetX - item.X) * ease;
                item.Y += (item.TargetY - item.Y) * ease;
                continue;
            }

            if (item.Mode == Mode.Done && _run == null)
            {
                // The server did not grind it up after all, so it comes back into view.
                item.Hold -= delta;
                if (item.Hold <= 0f)
                    item.Mode = Mode.Rest;
            }

            if (item.Mode != Mode.Tumble)
                continue;

            var radius = Math.Max(item.Info.Width, item.Info.Height) / 2f;
            item.VelocityY += Gravity * delta;
            item.X += item.VelocityX * delta;
            item.Y += item.VelocityY * delta;

            var wall = GrinderArt.Inset(GrinderArt.Round(item.Y + radius * 0.5f)) + radius * 0.7f;
            if (item.X < wall)
            {
                item.X = wall;
                item.VelocityX = MathF.Abs(item.VelocityX);
            }

            if (item.X > GlassWidth - wall)
            {
                item.X = GlassWidth - wall;
                item.VelocityX = -MathF.Abs(item.VelocityX);
            }

            if (item.Y < radius * 0.6f)
            {
                item.Y = radius * 0.6f;
                item.VelocityY = MathF.Abs(item.VelocityY) * 0.5f;
            }

            if (item.Y <= floor - radius * 0.7f)
                continue;

            item.Y = floor - radius * 0.7f;
            item.VelocityY = -(80f + _random.NextFloat(90f));
            item.VelocityX = _random.NextFloat(-75f, 75f);
            item.Turns = (item.Turns + 1 + _random.Next(2)) % 4;
        }
    }

    private void StepLoose(float delta)
    {
        for (var i = _gone.Count - 1; i >= 0; i--)
        {
            _gone[i].Leave += _reduced ? 1f : delta / LeaveTime;
            if (_gone[i].Leave >= 1f)
                _gone.RemoveAt(i);
        }

        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            particle.Age += delta;
            particle.Velocity.Y += particle.Gravity * delta;
            particle.Position += particle.Velocity * delta;
            var landed = particle.Lands && particle.Velocity.Y > 0f && particle.Position.Y > Floor - _massHeight * 0.8f;
            if (particle.Age < particle.Life && !landed)
            {
                _particles[i] = particle;
                continue;
            }

            _particles.RemoveAt(i);
            if (!particle.Scrap || _specks.Count >= MaxSpecks || _massHeight <= 0)
                continue;

            _specks.Add(new Speck
            {
                Angle = _random.NextFloat(MathF.Tau),
                Radius = 0.2f + _random.NextFloat(0.8f),
                Depth = _random.NextFloat(),
                Color = particle.Color,
                Size = _massKind == GrinderMassKind.Paste ? 2 : 1,
            });
        }

        for (var i = _drops.Count - 1; i >= 0; i--)
        {
            var drop = _drops[i];
            drop.Age += delta;
            drop.Position.Y += delta * 5f;
            if (drop.Age > 2.6f || _run == null)
                _drops.RemoveAt(i);
            else
                _drops[i] = drop;
        }
    }

    private void PaintMass()
    {
        if (_massHeight <= 0)
            return;

        var spinning = _run != null && !_reduced;
        _painter.Paint(_massKind, _massHeight, spinning, _clock, _run?.Shift ?? 0f);

        var tops = _painter.Tops;
        foreach (var speck in _specks)
        {
            if (MathF.Cos(speck.Angle) < -0.15f)
                continue;

            var x = GrinderArt.Round(Center + (Center - GrinderArt.JugTaper - 2) * speck.Radius * MathF.Sin(speck.Angle));
            if (x < 0 || x >= GlassWidth || tops[x] < 0)
                continue;

            var y = tops[x] + 2 + GrinderArt.Round(speck.Depth * Math.Max(0, Floor - 2 - tops[x] - 4));
            _painter.Square(x, y, speck.Size, speck.Size, Vector3.Lerp(speck.Color, _massColor, speck.Fade));
        }

        if (_massKind == GrinderMassKind.Juice)
        {
            for (var i = 0; i < 10; i++)
            {
                var angle = i * 0.63f + (_run?.Shift ?? 0f) * 0.05f;
                if (MathF.Cos(angle) < -0.1f)
                    continue;

                var x = GrinderArt.Round(Center + (Center - GrinderArt.JugTaper) * MathF.Sin(angle));
                if (x < 0 || x >= GlassWidth || tops[x] < 0)
                    continue;

                _painter.Square(x, tops[x] - 1, 4, 2, Vector3.Lerp(_massColor, White, 0.78f));
                _painter.Square(x + 1, tops[x] - 2, 2, 1, Vector3.Lerp(_massColor, White, 0.9f));
            }
        }

        var size = new Vector2i(GrinderMassPainter.Width, GrinderMassPainter.Height);
        _massTexture ??= _clyde.CreateBlankTexture<Rgba32>(size, "grinder-mass");
        _massTexture.SetSubImage(Vector2i.Zero, size, new ReadOnlySpan<Rgba32>(_painter.Pixels));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        var layered = RenderLayer(handle, canvas.Scale);

        var shake = Shake();
        var x = shake.X - Area.Left;
        var y = shake.Y - Area.Top;
        canvas.Texture(_jug, JugAt.X + x, JugAt.Y + y);

        if (_run != null && !_reduced)
        {
            var axis = Left + Center + x;
            var floor = Top + Floor + y;
            canvas.Rect(axis - 34, floor - 5, 68, 2, BladeBlur.WithAlpha(0.5f));
            canvas.Rect(axis - 28, floor - 7, 56, 1, BladeBlur.WithAlpha(0.22f));
            canvas.Rect(axis + GrinderArt.Round(MathF.Sin(_clock * 30f) * 26f) - 5, floor - 6, 10, 3, Color.White.WithAlpha(0.8f));
            canvas.Texture(_hub, HubAt.X + x, HubAt.Y + y);
        }
        else
        {
            canvas.Texture(_blades, BladesAt.X + x, BladesAt.Y + y);
        }

        if (layered && _layer != null)
        {
            foreach (var (from, to, inset) in Bands)
            {
                var source = UIBox2.FromDimensions(inset * _layerScale, (from + Headroom) * _layerScale, (GlassWidth - inset * 2) * _layerScale, (to - from) * _layerScale);
                canvas.Stretch(_layer.Texture, source, Left + inset + x, Top + from + y, GlassWidth - inset * 2, to - from);
            }
        }

        if (_massHeight > 0 && _massTexture != null)
            canvas.Texture(_massTexture, Left + x, Top + y);

        foreach (var particle in _particles)
        {
            var alpha = particle.Fades ? 1f - particle.Age / particle.Life : 1f;
            Dot(canvas, x, y, particle.Position, particle.Size, particle.Size, GrinderArt.ToColor(particle.Color, alpha));
        }

        foreach (var drop in _drops)
        {
            Dot(canvas, x, y, drop.Position, 2, 3, GrinderArt.ToColor(drop.Color, drop.Age > 1.8f ? 0.4f : 0.85f));
            Dot(canvas, x, y, drop.Position - new Vector2(0f, 2f), 1, 2, GrinderArt.ToColor(drop.Color, 0.4f));
        }

        canvas.Texture(_glass, GlassAt.X + x, GlassAt.Y + y);
        canvas.Texture(_lidTexture, LidAt.X + x, LidAt.Y - GrinderArt.Round(_lid * LidLift) + y);
    }

    /// <summary>
    /// The jug trembles while the blades work, like the grinder itself does in the world. A dry, hard load shakes it harder.
    /// </summary>
    private Vector2i Shake()
    {
        if (_run == null || _reduced)
            return Vector2i.Zero;

        var reach = _massKind == GrinderMassKind.Powder ? 2 : 1;
        var tick = (int) (_clock * ShakeRate);
        var x = (int) (GrinderMassPainter.Hash(tick, 1, 77) * (reach * 2 + 1)) - reach;
        var y = reach > 1 ? (int) (GrinderMassPainter.Hash(tick, 2, 77) * 3) - 1 : 0;
        return new Vector2i(x, y);
    }

    /// <summary>
    /// Draws a small rectangle inside the jug, unless a part of it would land outside the glass.
    /// </summary>
    private static void Dot(PixelCanvas canvas, int offsetX, int offsetY, Vector2 position, int width, int height, Color color)
    {
        var x = GrinderArt.Round(position.X);
        var y = GrinderArt.Round(position.Y);
        var inset = GrinderArt.Inset(y + height - 1);
        if (y < 0 || y + height > Floor || x < inset || x + width > GlassWidth - inset)
            return;

        canvas.Rect(Left + x + offsetX, Top + y + offsetY, width, height, color);
    }

    /// <summary>
    /// Draws the things in the jug on a layer of their own, which is then cut to the shape of the glass.
    /// </summary>
    private bool RenderLayer(DrawingHandleScreen handle, int scale)
    {
        _drawOrder.Clear();
        foreach (var item in _items)
        {
            if (item.Mode == Mode.Done)
                continue;

            var at = _drawOrder.Count;
            while (at > 0 && Before(item, _drawOrder[at - 1]))
            {
                at--;
            }

            _drawOrder.Insert(at, item);
        }

        if (_drawOrder.Count == 0 && _gone.Count == 0)
            return false;

        if (_layer == null || _layerScale != scale)
        {
            _layer?.Dispose();
            _layerScale = scale;
            _layer = _clyde.CreateRenderTarget(
                new Vector2i(GlassWidth, Floor + Headroom) * scale,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                name: "grinder-jug");
        }

        _handle = handle;
        handle.RenderInRenderTarget(_layer, _drawItems, Color.Transparent);
        _handle = null;
        return true;
    }

    // The thing the blades are working on is drawn first, the rest from the top of the jug down.
    private static bool Before(Item item, Item other)
    {
        var shredding = item.Mode == Mode.Shred;
        if (shredding != (other.Mode == Mode.Shred))
            return shredding;

        return item.Y < other.Y;
    }

    private void DrawItems()
    {
        if (_handle is not { } handle)
            return;

        handle.SetTransform(Matrix3x2.Identity);
        foreach (var item in _drawOrder)
        {
            DrawItem(handle, item);
        }

        foreach (var item in _gone)
        {
            if (item.Mode != Mode.Done)
                DrawItem(handle, item);
        }
    }

    private void DrawItem(DrawingHandleScreen handle, Item item)
    {
        if (_entMan.Deleted(item.Uid) || !_entMan.TryGetComponent(item.Uid, out SpriteComponent? sprite))
            return;

        var x = GrinderArt.Round(item.X);
        var y = GrinderArt.Round(item.Y + item.Sink);
        if (item.Leave >= 0f)
            y -= GrinderArt.Round(item.Leave * item.Leave * LeaveRise);
        else if (_interactive && (item.Uid == Hovered || item.Uid == Highlighted))
            y -= HoverLift;

        // The thing turns around the middle of its visible part, which is rarely the middle of its sprite.
        var offset = new Vector2(-item.Info.CenterX, -item.Info.CenterY) * Scale;
        for (var i = 0; i < item.Turns; i++)
        {
            offset = new Vector2(-offset.Y, offset.X);
        }

        var position = (new Vector2(x, y + Headroom) + offset) * _layerScale;
        handle.DrawEntity(item.Uid, position, new Vector2(Scale * _layerScale), Angle.FromDegrees(-90 * item.Turns), overrideDirection: Direction.South, sprite: sprite);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _layer?.Dispose();
        _layer = null;
        _massTexture?.Dispose();
        _massTexture = null;
    }

    protected override bool HasPoint(Vector2 point)
    {
        var art = ArtPoint(point * UIScale);
        if (Handle.Contains(art))
            return true;

        var lidTop = LidAt.Y - GrinderArt.Round(_lid * LidLift);
        if (art.Y >= lidTop && art.Y < JugAt.Y)
            return art.X >= LidAt.X && art.X < LidAt.X + _lidTexture.Width;

        var row = art.Y - Top;
        var inset = GrinderArt.Inset(row) - 5;
        return art.Y >= JugAt.Y && row < GrinderArt.JugHeight + 4 && art.X >= Left + inset && art.X < Left + GlassWidth - inset;
    }

    private Vector2i ArtPoint(Vector2 pixel)
    {
        var scale = PixelCanvas.GetScale(UIScale);
        return new Vector2i((int) MathF.Floor(pixel.X / scale) + Area.Left, (int) MathF.Floor(pixel.Y / scale) + Area.Top);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        _mouse = args.RelativePixelPosition;
        UpdateHover();
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        _mouse = null;
        UpdateHover();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || !_interactive)
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
        if (_mouse is { } mouse && _interactive)
        {
            var point = ArtPoint(mouse);
            for (var i = _drawOrder.Count - 1; i >= 0 && hovered == null; i--)
            {
                var item = _drawOrder[i];
                var info = item.Info;
                var box = UIBox2i.FromDimensions(
                    Left + GrinderArt.Round(item.X - info.Width / 2f),
                    Top + GrinderArt.Round(item.Y - info.Height / 2f),
                    info.Width,
                    info.Height);
                if (box.Contains(point))
                    hovered = item.Uid;
            }
        }

        DefaultCursorShape = hovered == null ? CursorShape.Arrow : CursorShape.Hand;
        if (hovered == Hovered)
            return;

        Hovered = hovered;
        ToolTip = hovered != null && _entMan.TryGetComponent(hovered.Value, out MetaDataComponent? meta) ? meta.EntityName : null;
        HideTooltip();
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

    private enum Mode : byte
    {
        Rest,
        Tumble,
        Shred,
        Done,
    }

    private sealed class Item
    {
        public EntityUid Uid;
        public GrinderSpriteInfo Info = default!;
        public Mode Mode;
        public float X;
        public float Y;
        public float VelocityX;
        public float VelocityY;
        public float TargetX;
        public float TargetY;
        public float Sink;
        public float Leave = -1f;
        public float Hold;
        public int Turns;
        public bool Fresh;
    }

    private sealed class Run
    {
        public GrinderProgram Program;
        public float Total;
        public float Pour;
        public float Shift;
    }

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector3 Color;
        public float Life;
        public float Age;
        public float Gravity;
        public int Size;
        public bool Lands;
        public bool Scrap;
        public bool Fades;
    }

    private struct Speck
    {
        public Vector3 Color;
        public float Angle;
        public float Radius;
        public float Depth;
        public float Fade;
        public int Size;
    }

    private struct Drop
    {
        public Vector2 Position;
        public Vector3 Color;
        public float Age;
    }
}

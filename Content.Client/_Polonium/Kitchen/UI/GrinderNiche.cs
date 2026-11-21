using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Random;

namespace Content.Client._Polonium.Kitchen.UI;

/// <summary>
/// The niche under the spout: the vessel on the tray and the stream that runs into it.
/// </summary>
public sealed partial class GrinderNiche : Control
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const int Left = 70;
    private const int Top = 206;

    public static readonly UIBox2i Area = UIBox2i.FromDimensions(Left, Top, 76, 80);

    private const int Axis = GrinderArt.SpoutX - Left;
    private const int Spout = GrinderArt.SpoutBottom - Top;
    private const int Tray = GrinderArt.TrayTop - Top;
    private const int StreamEnd = Tray - 12;
    private const int Scale = GrinderArt.SpriteScale;
    private const int PictureSize = 32 * Scale;
    private const int DropIn = 10;
    private const int LeaveRise = 8;
    private const float DropTime = 0.16f;
    private const float LeaveTime = 0.2f;
    private const float StreamSpeed = 24f;
    private const float SplashRate = 30f;
    private const float GhostAlpha = 0.45f;

    private static readonly UIBox2i Stand = UIBox2i.FromDimensions(72 - Left, 232 - Top, 72, 52);
    private static readonly Color Outline = Color.White.WithAlpha(0.22f);
    private static readonly Vector3 White = new(255f);
    private static readonly LocId TakeTip = "polonium-grinder-vessel-take";
    private static readonly LocId PutTip = "polonium-grinder-vessel-put";
    private static readonly LocId NoHandTip = "polonium-grinder-hand-empty";

    private readonly GrinderSpriteProbe _probe;
    private readonly GrinderMassPainter _painter;
    private readonly PixelParticles _splash;
    private readonly Action _drawPicture;
    private readonly string _takeTip;
    private readonly string _noHandTip;
    private IRenderTexture? _picture;
    private DrawingHandleScreen? _handle;
    private SpriteComponent? _pictureSprite;
    private EntityUid _pictureOf;
    private EntityUid? _vessel;
    private EntityUid? _leaving;
    private EntityUid? _hand;
    private GrinderStream _stream;
    private string _putTip = string.Empty;
    private string _putName = string.Empty;
    private float _settled = 1f;
    private float _leave;
    private float _clock;
    private int _pictureScale;
    private bool _busy;
    private bool _reduced;
    private bool _opened;
    private bool _hovered;
    private bool _clickable;

    /// <summary>
    /// Raised by a click. It carries true when the niche is empty and the held vessel should go in.
    /// </summary>
    public event Action<bool>? OnPressed;

    /// <param name="painter">Tells the stream which colours the grains of a dry mass have.</param>
    public GrinderNiche(GrinderSpriteProbe probe, GrinderMassPainter painter)
    {
        IoCManager.InjectDependencies(this);

        _probe = probe;
        _painter = painter;
        _drawPicture = DrawPicture;
        _takeTip = Loc.GetString(TakeTip);
        _noHandTip = Loc.GetString(NoHandTip);

        MouseFilter = MouseFilterMode.Pass;

        _splash = new PixelParticles();
        AddChild(_splash);
    }

    /// <summary>
    /// Brings the niche up to date with the grinder and moves everything in it on by one frame.
    /// </summary>
    /// <param name="stream">The mass that leaves the jug.</param>
    public void Advance(GrinderDeviceModel model, GrinderStream stream, float delta, bool reduced)
    {
        _clock += delta;
        _reduced = reduced;
        _stream = stream;
        _busy = model.Busy;
        _hand = model.HandVessel;

        if (model.Vessel != _vessel)
        {
            var shown = _opened && !reduced;
            if (_vessel != null && shown)
            {
                _leaving = _vessel;
                _leave = 0f;
            }

            _vessel = model.Vessel;
            _settled = _vessel != null && shown ? 0f : 1f;
        }

        _opened = true;
        _settled = MathF.Min(1f, _settled + delta / DropTime);
        if (_leaving != null)
        {
            _leave += delta / LeaveTime;
            if (_leave >= 1f || reduced)
                _leaving = null;
        }

        if (!reduced && stream.Pour > 0.1f && stream.Pour < 0.9f && _random.Prob(MathF.Min(1f, delta * SplashRate)))
        {
            _splash.Add(
                new Vector2(Axis + _random.NextFloat(-3f, 3f), StreamEnd - 2f),
                new Vector2(_random.NextFloat(-15f, 15f), -20f - _random.NextFloat(25f)),
                0.25f,
                1,
                GrinderArt.ToColor(Vector3.Lerp(stream.Color, White, 0.3f)),
                gravity: 200f);
        }

        UpdateHint();
    }

    private void UpdateHint()
    {
        _clickable = _vessel != null ? !_busy : _hand != null;
        var shape = _clickable ? CursorShape.Hand : CursorShape.Arrow;
        if (DefaultCursorShape != shape)
            DefaultCursorShape = shape;

        if (_vessel != null)
        {
            this.SetLiveToolTip(_busy ? null : _takeTip);
            return;
        }

        if (_hand is not { } hand || !_entMan.TryGetComponent(hand, out MetaDataComponent? meta))
        {
            this.SetLiveToolTip(_noHandTip);
            return;
        }

        if (meta.EntityName != _putName)
        {
            _putName = meta.EntityName;
            _putTip = Loc.GetString(PutTip, ("name", _putName));
        }

        this.SetLiveToolTip(_putTip);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        DrawStream(canvas);

        if (_vessel is { } vessel)
        {
            var above = 1f - _settled;
            DrawVessel(handle, canvas, vessel, Tray - GrinderArt.Round(above * above * DropIn), 0.4f + 0.6f * _settled);
        }
        else if (_leaving == null)
        {
            if (_hand is { } hand && _hovered)
                DrawVessel(handle, canvas, hand, Tray, GhostAlpha);
            else
                DrawOutline(canvas);
        }

        if (_leaving is { } leaving)
            DrawVessel(handle, canvas, leaving, Tray - GrinderArt.Round(_leave * LeaveRise), 1f - _leave);
    }

    private void DrawStream(PixelCanvas canvas)
    {
        if (_reduced || _stream.Pour <= 0f || _stream.Pour >= 1f)
            return;

        var step = (int) MathF.Floor(_clock * StreamSpeed);
        var color = _stream.Color;
        for (var y = Spout; y < StreamEnd; y++)
        {
            switch (_stream.Kind)
            {
                case GrinderMassKind.Paste:
                    var width = ((y - step) & 7) < 5 ? 5 : 3;
                    canvas.Rect(Axis - width / 2, y, width, 1, GrinderArt.ToColor(color));
                    canvas.Rect(Axis - width / 2, y, 1, 1, GrinderArt.ToColor(Vector3.Lerp(color, White, 0.3f)));
                    break;
                case GrinderMassKind.Powder:
                    for (var grain = 0; grain < 4; grain++)
                    {
                        var pick = GrinderMassPainter.Hash(grain, y - step * 3, 41);
                        if (pick < 0.5)
                            canvas.Rect(Axis - 2 + (int) (pick * 40) % 5, y, 1, 1, GrinderArt.ToColor(_painter.CrumbColor(GrinderMassPainter.Hash(grain, y - step * 3, 43))));
                    }

                    break;
                default:
                    canvas.Rect(Axis - 2, y, 4, 1, GrinderArt.ToColor(color));
                    canvas.Rect(Axis - 2, y, 1, 1, GrinderArt.ToColor(Vector3.Lerp(color, White, 0.5f)));
                    canvas.Rect(Axis + 1, y, 1, 1, GrinderArt.ToColor(Vector3.Lerp(color, Vector3.Zero, 0.2f)));
                    break;
            }
        }
    }

    /// <summary>
    /// Marks the place where a vessel goes while the tray is empty.
    /// </summary>
    private static void DrawOutline(PixelCanvas canvas)
    {
        for (var i = 0; i < 5; i++)
        {
            canvas.Rect(Axis - 14 + i * 6, Tray - 30, 3, 1, Outline);
            canvas.Rect(Axis - 14 + i * 6, Tray - 3, 3, 1, Outline);
            canvas.Rect(Axis - 15, Tray - 29 + i * 6, 1, 3, Outline);
            canvas.Rect(Axis + 14, Tray - 29 + i * 6, 1, 3, Outline);
        }
    }

    /// <summary>
    /// Draws the vessel so that the visible part of its sprite stands on the given line under the spout.
    /// </summary>
    private void DrawVessel(DrawingHandleScreen handle, PixelCanvas canvas, EntityUid uid, int tray, float alpha)
    {
        if (_entMan.Deleted(uid) || !_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
            return;

        var info = _probe.Get(uid);
        var x = Axis - info.CenterX * Scale;
        var y = tray - (info.Bottom + 1) * Scale;
        if (alpha >= 1f)
        {
            canvas.Entity(uid, sprite, x, y, new Vector2(Scale));
            return;
        }

        // The engine draws an entity only fully opaque, so a vessel that fades goes through a picture of its own.
        if (_picture == null || _pictureScale != canvas.Scale)
        {
            _picture?.Dispose();
            _pictureScale = canvas.Scale;
            _picture = _clyde.CreateRenderTarget(
                new Vector2i(PictureSize, PictureSize) * canvas.Scale,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                name: "grinder-vessel");
        }

        _pictureOf = uid;
        _pictureSprite = sprite;
        _handle = handle;
        handle.RenderInRenderTarget(_picture, _drawPicture, Color.Transparent);
        _handle = null;
        _pictureSprite = null;

        var source = UIBox2.FromDimensions(Vector2.Zero, _picture.Size);
        canvas.Stretch(_picture.Texture, source, x - PictureSize / 2, y - PictureSize / 2, PictureSize, PictureSize, Color.White.WithAlpha(alpha));
    }

    private void DrawPicture()
    {
        if (_handle is not { } handle || _picture == null)
            return;

        handle.SetTransform(Matrix3x2.Identity);
        handle.DrawEntity(_pictureOf, (Vector2) _picture.Size / 2f, new Vector2(Scale * _pictureScale), Angle.Zero, overrideDirection: Direction.South, sprite: _pictureSprite);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _picture?.Dispose();
        _picture = null;
    }

    protected override bool HasPoint(Vector2 point)
    {
        var scale = PixelCanvas.GetScale(UIScale);
        var art = new Vector2i((int) MathF.Floor(point.X * UIScale / scale), (int) MathF.Floor(point.Y * UIScale / scale));
        if (_vessel is not { } vessel)
            return Stand.Contains(art);

        var info = _probe.Get(vessel);
        return UIBox2i.FromDimensions(Axis - info.Width / 2 - 2, Tray - info.Height - 2, info.Width + 4, info.Height + 2).Contains(art);
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();

        _hovered = true;
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        _hovered = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || !_clickable)
            return;

        OnPressed?.Invoke(_vessel == null);
        args.Handle();
    }
}

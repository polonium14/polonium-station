using System.Text;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Lathe.UI;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// The lube tank bolted to the side of the console. Its level moves frame by frame, like a hand-drawn animation.
/// </summary>
public sealed partial class LatheTank : Control
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    public const int ArtWidth = 40;
    public const int ArtHeight = 198;

    private const int GlassTop = 45;
    private const int GlassHeight = 144;
    private const int LiquidLeft = 5;
    private const int LiquidWidth = 30;
    private const int TagCenter = 20;
    private const int TagTop = 65;
    private const int TagHeight = 9;
    private const float FrameTime = 0.13f;
    private const int MaxBubbles = 6;
    private const int BubblesPerItem = 2;

    private struct Bubble
    {
        public int X;
        public float Y;
        public float Speed;
        public int Size;
    }

    private readonly LatheConsoleModel _model;
    private readonly PixelFont _font;
    private readonly LatheTextures _textures;
    private readonly List<Bubble> _bubbles = new();
    private readonly string _tag;
    private readonly int _tagWidth;

    private float _clock = FrameTime;
    private float _volume = -1f;
    private int _shown = -1;
    private Color _color;

    public LatheTank(LatheConsoleModel model, PixelFont font, LatheTextures textures)
    {
        IoCManager.InjectDependencies(this);

        _model = model;
        _font = font;
        _textures = textures;
        _tag = Loc.GetString("polonium-lathe-tank-tag").ToUpperInvariant();
        _tagWidth = _font.Measure(_tag) - 1 + 6;

        MouseFilter = MouseFilterMode.Stop;
        TooltipSupplier = _ => new RecipeTooltip(TooltipText());
    }

    private int Wanted
    {
        get
        {
            if (_model.LubeSolution is not { } solution || solution.MaxVolume <= 0)
                return 0;

            return (int) MathF.Round(Math.Clamp(solution.Volume.Float() / solution.MaxVolume.Float(), 0f, 1f) * GlassHeight);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_model.LubeSolution is not { } solution)
            return;

        var volume = solution.Volume.Float();
        if (_shown < 0)
        {
            _shown = Wanted;
            _volume = volume;
        }

        if (volume > 0f)
            _color = solution.GetColor(_proto);

        if (volume < _volume && !_model.ReducedMotion)
        {
            for (var i = 0; i < BubblesPerItem && _bubbles.Count < MaxBubbles; i++)
            {
                _bubbles.Add(new Bubble
                {
                    X = 2 + _random.Next(25),
                    Y = _random.NextFloat(6f),
                    Speed = 40f + _random.NextFloat(30f),
                    Size = i == 0 ? 2 : 1,
                });
            }
        }

        _volume = volume;
        if (_model.ReducedMotion)
        {
            _shown = Wanted;
            _bubbles.Clear();
            return;
        }

        _clock += args.DeltaSeconds;
        if (_clock < FrameTime)
            return;

        _clock %= FrameTime;
        var gap = Wanted - _shown;
        if (gap != 0)
            _shown += Math.Sign(gap) * Math.Min(Math.Abs(gap), Math.Max(1, (Math.Abs(gap) + 3) / 4));

        for (var i = _bubbles.Count - 1; i >= 0; i--)
        {
            var bubble = _bubbles[i];
            bubble.Y += bubble.Speed * FrameTime;
            if (bubble.Y > _shown - 2)
                _bubbles.RemoveAt(i);
            else
                _bubbles[i] = bubble;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        canvas.Texture(_textures.TankBack, 0, 0);

        if (_shown > 0)
        {
            var top = GlassTop + GlassHeight - _shown;
            var light = Color.InterpolateBetween(_color, Color.White, 0.3f);
            var surface = Color.InterpolateBetween(_color, Color.White, 0.55f);
            var shade = Color.InterpolateBetween(_color, Color.Black, 0.2f);
            var dark = Color.InterpolateBetween(_color, Color.Black, 0.36f);
            canvas.Rect(LiquidLeft, top, LiquidWidth, _shown, _color);
            canvas.Rect(LiquidLeft, top, 1, _shown, shade);
            canvas.Rect(LiquidLeft + 4, top, 3, _shown, light);
            canvas.Rect(LiquidLeft + LiquidWidth - 10, top, 5, _shown, shade);
            canvas.Rect(LiquidLeft + LiquidWidth - 5, top, 5, _shown, dark);
            canvas.Rect(LiquidLeft, top, LiquidWidth, 1, surface);
            if (_shown < GlassHeight)
            {
                canvas.Rect(LiquidLeft, top - 1, 1, 1, surface);
                canvas.Rect(LiquidLeft + LiquidWidth - 1, top - 1, 1, 1, surface);
            }

            foreach (var bubble in _bubbles)
            {
                var y = GlassTop + GlassHeight - 1 - (int) MathF.Round(bubble.Y);
                if (y > top + 1)
                    canvas.Rect(LiquidLeft + bubble.X, y, bubble.Size, bubble.Size, surface);
            }
        }

        canvas.Texture(_textures.TankFront, 0, 0);

        var left = TagCenter - _tagWidth / 2;
        canvas.Rect(left, TagTop, _tagWidth, TagHeight, LathePalette.Outline);
        canvas.Rect(left + 1, TagTop + 1, _tagWidth - 2, TagHeight - 2, LathePalette.Panel);
        canvas.Text(_font, left + 3, TagTop, _tag, LathePalette.Label);
    }

    private string TooltipText()
    {
        var text = new StringBuilder();
        text.Append("[bold]").Append(Loc.GetString("polonium-lathe-tank-title")).Append("[/bold]");
        if (_model.LubeSolution is not { } solution || _model.Lube is not { } lube)
            return text.ToString();

        foreach (var reagent in solution.Contents)
        {
            if (!_proto.Resolve<ReagentPrototype>(reagent.Reagent.Prototype, out var prototype))
                continue;

            text.Append('\n').Append(Loc.GetString("polonium-lathe-tank-reagent",
                ("reagent", FormattedMessage.EscapeText(prototype.LocalizedName)),
                ("amount", reagent.Quantity.Float())));
        }

        text.Append('\n').Append(solution.Volume > 0
            ? Loc.GetString("polonium-lathe-tank-total", ("amount", solution.Volume.Float()), ("max", solution.MaxVolume.Float()))
            : Loc.GetString("polonium-lathe-tank-empty"));

        var cost = lube.Cost;
        var modifiers = lube.Modifiers;
        text.Append("\n[italic]").Append(Loc.GetString("polonium-lathe-tank-cost", ("amount", cost.Float())));
        foreach (var (id, modifier) in modifiers)
        {
            if (modifier == 1f || !_proto.Resolve(id, out var prototype))
                continue;

            text.Append(' ').Append(Loc.GetString(modifier < 1f ? "polonium-lathe-tank-faster" : "polonium-lathe-tank-slower",
                ("reagent", FormattedMessage.EscapeText(prototype.LocalizedName))));
        }

        return text.Append("[/italic]").ToString();
    }
}

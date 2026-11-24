using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Chemistry.UI;

/// <summary>
/// Containers that have just been taken out of the dispenser: each rises from its place and fades.
/// </summary>
public sealed class BarLeavingLayer : Control
{
    private const float Time = 0.5f;
    private const int Rise = 4;
    private const float Fade = 0.25f;
    private const float FaintestAlpha = 0.15f;

    private static readonly Angle UpsideDown = Angle.FromDegrees(180);

    private readonly BarSpriteProbe _probe;
    private readonly BarEntityPainter _painter;
    private readonly List<Leaving> _leaving = new();

    public BarLeavingLayer(BarSpriteProbe probe, BarEntityPainter painter)
    {
        _probe = probe;
        _painter = painter;

        MouseFilter = MouseFilterMode.Ignore;
    }

    /// <summary>
    /// Shows a container on its way out.
    /// </summary>
    /// <param name="axis">The column the container stands on.</param>
    /// <param name="foot">The row under the container: under its neck when it stands upside down.</param>
    public void Add(EntityUid container, int axis, int foot, bool upsideDown)
    {
        _leaving.Add(new Leaving(container, axis, foot, upsideDown));
    }

    /// <summary>
    /// Moves the containers on by one frame.
    /// </summary>
    public void Advance(float delta, bool reduced)
    {
        if (reduced)
        {
            _leaving.Clear();
            return;
        }

        for (var i = _leaving.Count - 1; i >= 0; i--)
        {
            _leaving[i].Age += delta;
            if (_leaving[i].Age >= Time)
                _leaving.RemoveAt(i);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_leaving.Count == 0)
            return;

        var canvas = new PixelCanvas(handle, UIScale);
        foreach (var leaving in _leaving)
        {
            var frame = BarDispenserArt.Frame(leaving.Age);
            var info = _probe.Get(leaving.Container);
            if (!info.Measured)
                continue;

            var foot = leaving.Foot - frame * Rise;
            var alpha = MathF.Max(FaintestAlpha, 1f - frame * Fade);
            if (leaving.UpsideDown)
                _painter.Draw(handle, canvas, leaving.Container, BarDispenserArt.Round(leaving.Axis + info.Center), foot + info.Top, 1, UpsideDown, alpha);
            else
                _painter.Draw(handle, canvas, leaving.Container, BarDispenserArt.Round(leaving.Axis - info.Center), foot - info.Bottom - 1, 1, Angle.Zero, alpha);
        }
    }

    private sealed class Leaving(EntityUid container, int axis, int foot, bool upsideDown)
    {
        public readonly EntityUid Container = container;
        public readonly int Axis = axis;
        public readonly int Foot = foot;
        public readonly bool UpsideDown = upsideDown;
        public float Age;
    }
}
